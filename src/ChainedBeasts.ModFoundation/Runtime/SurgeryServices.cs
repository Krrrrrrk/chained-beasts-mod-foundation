using System;
using System.Collections.Generic;
using System.Linq;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Logic;
using HarmonyLib;
using Unity.Netcode;

namespace ChainedBeasts.ModFoundation.Runtime;

/// <summary>One native surgeon-offer/completion adapter shared by independently registered content.</summary>
public static class SurgeryServices
{
    internal sealed class Offer { internal string Key; internal int Price; internal Func<Surgery> Factory; internal bool Force; }
    internal static readonly Dictionary<string, Offer> Offers = new(StringComparer.Ordinal);
    public static void Register(string key, int price, Func<Surgery> factory, bool forceOffer = false)
    {
        if (price < 0 || factory is null || !FoundationServices.Content.TryGet(key, out var content) || content.Category != ContentCategory.Surgery)
            throw new ArgumentException("Register a claimed surgery identity with a nonnegative price and native Surgery factory.");
        if (!Offers.TryAdd(key, new Offer { Key = key, Price = price, Factory = factory, Force = forceOffer })) throw new InvalidOperationException("Duplicate surgery provider: " + key);
    }
    public static bool IsOwned(PlayerData data, string key) => data?.previousSurgeries is not null &&
        FoundationServices.Content.TryGet(key, out var entry) && entry.Category == ContentCategory.Surgery && data.previousSurgeries.Contains((SurgeryType)entry.NumericId);
}

internal static class NativeSurgeries
{
    internal sealed class Operation
    {
        internal Operation Parent;
        internal PlayerData Data;
        internal ContentDescriptor Entry;
        internal Surgery Surgery;
        internal int Money, Maximum;
        internal float StaminaMaximum;
        internal SurgeryCompletion Receipt = new();
    }
    [ThreadStatic] private static Operation _operation;
    internal static PlayerData OperatingPlayer => _operation?.Data;
    internal static void Install(Harmony harmony)
    {
        void Patch(Type type, string name, string before = null, string after = null, string final = null, params Type[] args) =>
            FeaturePatcher.Patch(harmony, typeof(NativeSurgeries), FeaturePatcher.RequireMethod(type, name, args), before, after, final);
        Patch(typeof(SurgeryGenerator), nameof(SurgeryGenerator.GetSurgeries), after: nameof(Offers), args: new[] { typeof(PlayerData), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(RunDataManager), typeof(Il2CppSystem.Collections.Generic.Dictionary<InjuryType, int>) });
        Patch(typeof(RunDataManager), nameof(RunDataManager.PerformSurgeryRpc), nameof(Before), nameof(After), nameof(Finally), typeof(int), typeof(int), typeof(int));
        Patch(typeof(PlayerData), nameof(PlayerData.ModifyAwardStat), after: nameof(Completed), args: new[] { typeof(AwardType), typeof(int) });
        FoundationServices.CheckpointCapturing += _ => { if (_operation?.Entry is not null) throw new InvalidOperationException("Surgery completion has not returned; checkpoint capture refused until ownership is committed."); };
    }
    private static void Offers(PlayerData playerData, int numberOfOptions, int remainingMoney, RunDataManager runDataManager, Il2CppSystem.Collections.Generic.List<Surgery> __result)
    {
        if (!SessionAccess.CanUseDeveloperTools || playerData is null || __result is null) return;
        var providers = SurgeryServices.Offers.Values.Where(offer => FoundationServices.Content.TryGet(offer.Key, out var entry) && entry.Enabled &&
            !SurgeryServices.IsOwned(playerData, offer.Key) && remainingMoney >= offer.Price).OrderBy(offer => offer.Key, StringComparer.Ordinal).ToArray();
        if (providers.Length == 0) return;
        var force = Array.FindIndex(providers, offer => offer.Force);
        var seed = unchecked((runDataManager?.RunSeed ?? 0) ^ ((runDataManager?.CompletedLevels ?? 0) * 397) ^ (playerData.playerID * 7919));
        if (__result.Count == 0)
        {
            // A healthy/fully treated player may exhaust native candidates but still qualify for new enhancements.
            foreach (var choice in SurgeryOffers.Select(0, providers.Length, Math.Max(0, numberOfOptions), seed, force))
                __result.Add(Create(providers[choice]));
            return;
        }
        var slots = new List<int>(); var native = new List<Surgery>();
        // Preserve donation choices and the first native targeted cure, if present. Keep count and all native objects intact elsewhere.
        var protectedCure = false;
        for (var i = 0; i < __result.Count; i++)
        {
            var surgery = __result[i];
            if (surgery?.isDonation == true) continue;
            if (!protectedCure && surgery?.specialEffects?.Contains((SurgerySpecialEffect)0) == true) { protectedCure = true; continue; }
            slots.Add(i); native.Add(surgery);
        }
        var selected = SurgeryOffers.Select(native.Count, providers.Length, slots.Count, seed, force);
        for (var i = 0; i < selected.Length; i++)
        {
            var index = selected[i];
            if (index < native.Count) { __result[slots[i]] = native[index]; continue; }
            __result[slots[i]] = Create(providers[index - native.Count]);
        }
    }
    private static Surgery Create(SurgeryServices.Offer offer)
    {
        var replacement = offer.Factory();
        if (replacement is null || !FoundationServices.Content.TryGet(offer.Key, out var entry) || (int)replacement.surgeryType != entry.NumericId || replacement.moneyCost != offer.Price)
            throw new InvalidOperationException("Invalid surgery factory: " + offer.Key);
        return replacement;
    }
    private static bool Before(RunDataManager __instance, int playerIndex, int surgeryIndex, out Operation __state)
    {
        __state = new Operation { Parent = _operation };
        if (!SessionAccess.CanUseDeveloperTools || __instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute) return true;
        var data = __instance.GetPlayerDataByIndex(playerIndex);
        if (data?.surgeryOptions is null || surgeryIndex < 0 || surgeryIndex >= data.surgeryOptions.Count) return true;
        var surgery = data.surgeryOptions[surgeryIndex];
        __state.Data = data; __state.Surgery = surgery; _operation = __state;
        if (surgery is null) return true;
        var entry = FoundationServices.Content.Entries.FirstOrDefault(entry => entry.Category == ContentCategory.Surgery && entry.NumericId == (int)surgery.surgeryType);
        if (entry is null) return true;
        // Native RPC itself does not guard money or repeat ownership. Do so only for our registered content before charging anything.
        var providerPresent = SurgeryServices.Offers.TryGetValue(entry.Key, out var offer);
        if (!SurgeryEligibility.CanAcquire(entry.Enabled, providerPresent, SurgeryServices.IsOwned(data, entry.Key), offer?.Price ?? -1,
            surgery.moneyCost, __instance.Money, _operation.Parent?.Data?.Pointer == data.Pointer))
        { __instance.__rpc_exec_stage = (NetworkBehaviour.__RpcExecStage)0; return false; }
        __state.Entry = entry; __state.Money = __instance.Money; __state.Maximum = data.MaxHealth;
        var player = LivePlayer(data);
        if (player is not null) __state.StaminaMaximum = player.StaminaMax;
        return true;
    }
    private static void Completed(PlayerData __instance, AwardType awardType, int value) =>
        _operation?.Receipt.Observe(_operation.Data?.Pointer == __instance.Pointer, (int)awardType, value);
    private static void After(RunDataManager __instance, Operation __state)
    {
        if (__state?.Entry is null || !__state.Receipt.Commit(true, SurgeryServices.IsOwned(__state.Data, __state.Entry.Key), __instance.Money == __state.Money - __state.Surgery.moneyCost)) return;
        var data = __state.Data; var player = LivePlayer(data);
        // Native history is the sole ownership source and is already serialized in native checkpoints.
        data.previousSurgeries.Add((SurgeryType)__state.Entry.NumericId);
        var maximum = data.MaxHealth;
        if (maximum != __state.Maximum)
        {
            data.hp = HealthTransition.Scale(data.hp, __state.Maximum, maximum, data.MaxAttainableHealth);
            if (player?.IsServer == true) player.health.Value = HealthTransition.Scale(player.health.Value, __state.Maximum, maximum, player.MaxAttainableHealth);
        }
        if (player?.IsOwner == true && __state.StaminaMaximum > 0 && player.StaminaMax != __state.StaminaMaximum)
            player.stamina = (float)Math.Clamp(player.Stamina / __state.StaminaMaximum * player.StaminaMax, 0, player.StaminaMax);
        FoundationServices.Logger?.LogInfo("[Foundation] confirmed surgery completion: " + __state.Entry.Key + ", player=" + data.playerID);
    }
    internal static Player LivePlayer(PlayerData data) => PlayerManager.InstanceExists && PlayerManager.Instance.PlayerCount > 0 ? data?.PlayerObject : null;
    private static Exception Finally(Exception __exception, Operation __state) { _operation = __state?.Parent; return __exception; }
}
