using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.State;
using ChainedBeasts.ModFoundation.Stats;
using ChainedBeasts.ModFoundation.Networking;

namespace ChainedBeasts.ModFoundation.Runtime;

public sealed class NativeStatContext
{
    public PlayerData Data { get; }
    public Player Player { get; }
    public NetworkHealthObject Target { get; }
    public AttackRecord Attack { get; }
    public NativeStatContext(PlayerData data, Player player) : this(data, player, null, null) { }
    public NativeStatContext(PlayerData data, Player player, NetworkHealthObject target, AttackRecord attack)
    { Data = data; Player = player; Target = target; Attack = attack; }
}
public sealed class OwnershipChange
{
    public PlayerData Data { get; }
    public ContentDescriptor Content { get; }
    public int Before { get; }
    public int After { get; }
    public int HealthBefore { get; }
    public int RuntimeHealthBefore { get; }
    public bool HasRuntimeHealth { get; }
    public int MaximumBefore { get; }
    public OwnershipChange(PlayerData data, ContentDescriptor content, int before, int after, int healthBefore,
        int runtimeHealthBefore, bool hasRuntimeHealth, int maximumBefore)
    { Data = data; Content = content; Before = before; After = after; HealthBefore = healthBefore;
        RuntimeHealthBefore = runtimeHealthBefore; HasRuntimeHealth = hasRuntimeHealth; MaximumBefore = maximumBefore; }
}

public static class FoundationServices
{
    public static bool IsReady { get; internal set; }
    internal static ManualLogSource Logger;
    public static ContentRegistry Content { get; } = new(IsNative);
    public static ModifierPipeline<NativeStatContext> Modifiers { get; } = new();
    internal static readonly Dictionary<string, (Action<GameLibrary> Before, Action<GameLibrary> After)> LibraryCallbacks = new(StringComparer.Ordinal);
    public static event Action<OwnershipChange> OwnershipChanged;
    public static event Action RunReset;
    public static event Action<string> CheckpointRestored;
    public static event Action<string> BattleStarted;
    public static event Action<RunDataManager> CheckpointCapturing;
    public static event Action<CheckpointEnvelope> CheckpointValidating;
    public static event Action<PlayerData, PurchaseType, PurchaseType> EquipmentChanged;
    public static event Action GameplayTick;

    private static bool IsNative(ContentCategory category, int id) => category switch
    {
        ContentCategory.Skill => Enum.IsDefined(typeof(SkillType), id),
        ContentCategory.Equipment => Enum.IsDefined(typeof(PurchaseType), id),
        ContentCategory.Affliction => Enum.IsDefined(typeof(InjuryType), id),
        ContentCategory.Surgery => Enum.IsDefined(typeof(SurgeryType), id),
        _ => true
    };

    public static void RegisterLibraryCallbacks(string contentKey, Action<GameLibrary> before, Action<GameLibrary> after = null)
    {
        if (!Content.TryGet(contentKey, out var definition) || !definition.Enabled)
            throw new InvalidOperationException("Register an enabled identity before its native library callbacks.");
        if (definition.Category == ContentCategory.Surgery)
            throw new InvalidOperationException("Surgeries have no native GameLibrary registry; integrate their generator explicitly.");
        if (!LibraryCallbacks.TryAdd(contentKey, (before, after)))
            throw new InvalidOperationException("Duplicate library registration: " + contentKey);
    }

    public static void RegisterModifier(string contentKey, StatChannel channel, Func<NativeStatContext, double> factor)
    {
        if (!Content.TryGet(contentKey, out _)) throw new InvalidOperationException("Register content identity first.");
        Modifiers.Register(contentKey + "/" + channel, channel,
            context => Content.TryGet(contentKey, out var definition) && definition.Enabled ? factor(context) : 1d);
    }

    public static PlayerContentState PlayerState(PlayerData player) => NativePersistence.PlayerState(player);

    internal static void PublishOwnership(OwnershipChange value) => Publish(OwnershipChanged, action => action(value));
    internal static void PublishReset() => Publish(RunReset, action => action());
    internal static void PublishRestore(string key) => Publish(CheckpointRestored, action => action(key));
    internal static void PublishBattle(string key) => Publish(BattleStarted, action => action(key));
    internal static void PublishTick() => Publish(GameplayTick, action => action());
    // Save/restore validation is deliberately fail-closed, unlike advisory events.
    internal static void CaptureFeatures(RunDataManager manager) => CheckpointCapturing?.Invoke(manager);
    internal static void ValidateFeatures(CheckpointEnvelope envelope) => CheckpointValidating?.Invoke(envelope);
    internal static void PublishEquipment(PlayerData player, PurchaseType before, PurchaseType after) =>
        Publish(EquipmentChanged, action => action(player, before, after));

    private static void Publish<T>(T handlers, Action<T> invoke) where T : Delegate
    {
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList().Cast<T>())
            try { invoke(handler); }
            catch (Exception exception) { Logger?.LogError("[Foundation] lifecycle subscriber failed: " + exception); }
    }
}

public static class SessionAccess
{
    public static bool GameplayReady => CanUseDeveloperTools || MultiplayerServices.Ready;
    public static bool IsSoloMode
    {
        get
        {
            try { return GameManager.InstanceExists && GameManager.Instance.CurrentGameMode == GameMode.Solo; }
            catch { return false; }
        }
    }

    public static bool CanUseDeveloperTools
    {
        get
        {
            try
            {
                if (!NetworkSession.InstanceExists) return false;
                var session = NetworkSession.Instance;
                var manager = session?.runDataManager;
                var clients = session?.clientList?.clients;
                var clientCount = clients?.Count ?? 0;
                var localCount = clientCount == 1 && session.IsLocalClient(clients[0]) ? 1 : 0;
                return SoloSessionPolicy.Allows(IsSoloMode, session is not null, manager?.IsServer == true,
                    clientCount, localCount, manager?.playerDatas?.Count ?? 0);
            }
            catch { return false; }
        }
    }
}
