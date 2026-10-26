extern alias GameJson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.State;
using UnityEngine;
using NativeTask = Il2CppSystem.Threading.Tasks.Task;
using NativeJson = GameJson::Newtonsoft.Json.JsonConvert;

namespace ChainedBeasts.ModFoundation.Runtime;

/// <summary>The only game adapter for external checkpoint ownership and feature state.</summary>
internal static class NativePersistence
{
    private static CheckpointStore _store;
    private static string _savePath;
    private static int _noneIndex;
    private static readonly RunStateSession State = new();
    private static readonly Dictionary<string, CheckpointEnvelope> Captured = new(StringComparer.Ordinal);
    private static readonly List<WriteTicket> Writes = new();
    private static readonly HashSet<string> CaptureFailures = new(StringComparer.Ordinal);
    private static CheckpointEnvelope _restoring;
    internal static bool IsRestoring => _restoring is not null;

    private sealed class WriteTicket
    {
        internal NativeTask Task;
        internal List<CheckpointIdentity> Identities = new();
    }
    internal sealed class MarkerScope
    {
        internal List<(PlayerData.Checkpoint Checkpoint, ContentCategory Category, int Index, int Level, int Order)> Values = new();
    }
    internal sealed class PlayerRestoreScope
    {
        internal MarkerScope Markers;
        internal PlayerContentState State;
    }

    internal static void Install(Harmony harmony, ManualLogSource logger)
    {
        _noneIndex = NativeIndex(ContentCategory.Skill, (int)SkillType.None);
        _store = new CheckpointStore(Path.Combine(Application.persistentDataPath, "ModFoundation", "checkpoints"));
        _savePath = Path.Combine(Application.persistentDataPath, "SaveData");
        void Patch(Type type, string name, string prefix = null, string postfix = null, string finalizer = null, params Type[] parameters) =>
            FeaturePatcher.Patch(harmony, typeof(NativePersistence), FeaturePatcher.RequireMethod(type, name, parameters), prefix, postfix, finalizer);
        Patch(typeof(RunDataManager), nameof(RunDataManager.CreateCheckpoint), postfix: nameof(Capture));
        Patch(typeof(PlayerData.Checkpoint), nameof(PlayerData.Checkpoint.Validate), prefix: nameof(ValidateBefore), finalizer: nameof(ValidateFinally));
        Patch(typeof(NetworkSession), nameof(NetworkSession.RestoreCheckpoint), prefix: nameof(AllowRestore));
        Patch(typeof(RunDataManager), nameof(RunDataManager.RestoreCheckpoint), nameof(RestoreBefore), nameof(RestoreAfter), nameof(RestoreFinally), typeof(RunDataManager.Checkpoint));
        Patch(typeof(PlayerData), nameof(PlayerData.RestoreCheckpoint), nameof(PlayerRestoreBefore), nameof(PlayerRestoreAfter), nameof(PlayerRestoreFinally), typeof(PlayerData.Checkpoint));
        Patch(typeof(PlayerData), nameof(PlayerData.OnCheckpointSynced), nameof(SyncBefore), null, nameof(ValidateFinally), typeof(PlayerData.Checkpoint), typeof(bool));
        Patch(typeof(NetworkSession), nameof(NetworkSession.ClearCheckpoint), postfix: nameof(Cleared));
        Patch(typeof(RunDataManager), nameof(RunDataManager.Reset), prefix: nameof(Reset));
        Patch(typeof(RunDataManager), nameof(RunDataManager.StartNewRunRpc), postfix: nameof(NewRun), parameters: new[]
            { typeof(int), typeof(int), typeof(AscensionLevel), typeof(DifficultyConfig), typeof(int) });
        Patch(typeof(GameLevel), "OnStateChanged", postfix: nameof(LevelStateChanged), parameters: new[] { typeof(int) });
        foreach (var platform in new[] { typeof(Platforms.Steam), typeof(Platforms.Standalone) })
            Patch(platform, nameof(Platforms.Steam.WriteFile), nameof(WriteBefore), nameof(WriteAfter), null,
                typeof(string), typeof(Il2CppStructArray<byte>), typeof(IPlatform.Sku));
        logger.LogInfo("[Foundation] checkpoint bridge installed; sidecars publish only after successful native platform writes");
    }

    private static string SnapshotKey(CheckpointIdentity identity) => identity.RunKey + ":" + identity.Token + ":" + identity.NativeFingerprint;
    private static string Serialize(RunDataManager.Checkpoint checkpoint) => NativeJson.SerializeObject(checkpoint);
    private static CheckpointIdentity Identity(RunDataManager.Checkpoint checkpoint) => CheckpointJson.Identify(Serialize(checkpoint), "Solo", _noneIndex);
    private static CheckpointIdentity CurrentRun(RunDataManager manager) => new("Solo", manager.RunSeed, manager.runStartTime,
        0, new string('0', 64), PlayerIds(manager));

    private static int[] PlayerIds(RunDataManager manager)
    {
        var result = new int[manager.playerDatas?.Count ?? 0];
        for (var index = 0; index < result.Length; index++) result[index] = manager.playerDatas[index].playerID;
        return result;
    }
    private static PlayerData FindPlayer(RunDataManager manager, int id)
    {
        for (var index = 0; index < manager.playerDatas.Count; index++)
            if (manager.playerDatas[index].playerID == id) return manager.playerDatas[index];
        throw new InvalidOperationException("Checkpoint player does not match the live solo roster.");
    }

    private static void EnsureRun(RunDataManager manager)
    {
        var run = CurrentRun(manager);
        if (!State.Start(run.RunKey)) return;
        Captured.Clear();
        NativeOwnership.Reset();
        FoundationServices.PublishReset();
    }

    internal static PlayerContentState PlayerState(PlayerData player)
    {
        if (!SessionAccess.CanUseDeveloperTools || player is null)
            throw new InvalidOperationException("Persistent feature state requires a host-owned, single-player Solo run.");
        var manager = NetworkSession.Instance.runDataManager;
        if (FindPlayer(manager, player.playerID).Pointer != player.Pointer)
            throw new InvalidOperationException("Preview/stale PlayerData cannot mutate live persistent feature state.");
        EnsureRun(manager);
        if (!State.Players.TryGetValue(player.playerID, out var state)) State.Players.Add(player.playerID, state = new());
        return state;
    }

    private static void Capture(RunDataManager __instance, RunDataManager.Checkpoint __result)
    {
        if (!SessionAccess.IsSoloMode || !__instance.IsServer || __result?.playerDatas is null || __result.playerCount != 1) return;
        EnsureRun(__instance);
        try { NativeCombat.RequireSettledCheckpoint(); FoundationServices.CaptureFeatures(__instance); }
        catch { CaptureFailures.Add(CurrentRun(__instance).RunKey); throw; }
        if (!FoundationServices.Content.Entries.Any(value => value.Storage == OwnershipStorage.ExternalCheckpoint) &&
            !State.Players.Values.Any(value => value.Ownership.Count > 0 || value.Features.Count > 0)) return;
        var first = __result.playerDatas[0];
        if (first.skillLevels[_noneIndex] != 0 || first.skillOrder[_noneIndex] != 0)
            throw new InvalidOperationException("Foundation checkpoint discriminator conflicts with occupied native None storage.");
        var run = CurrentRun(__instance);
        var used = new HashSet<int>(Captured.Values.Where(value => value.Identity.RunKey == run.RunKey).Select(value => value.Identity.Token));
        var token = _store.AllocateToken(run, used);
        first.skillLevels[_noneIndex] = CheckpointEnvelope.Marker;
        first.skillOrder[_noneIndex] = token;
        var identity = Identity(__result);
        var envelope = new CheckpointEnvelope { Identity = identity, LegacyImported = State.LegacyImported };
        for (var index = 0; index < __result.playerCount; index++)
        {
            var checkpointPlayer = __result.playerDatas[index];
            var player = FindPlayer(__instance, checkpointPlayer.playerID);
            var state = State.Players.TryGetValue(player.playerID, out var prior) ? Clone(prior) : new PlayerContentState();
            foreach (var definition in FoundationServices.Content.Entries.Where(value => value.Enabled))
            {
                state.Ownership.Remove(definition.Key);
                var level = Owned(player, definition);
                if (level <= 0) continue;
                var order = definition.Category switch
                {
                    ContentCategory.Skill => player.skillOrder.ContainsKey((SkillType)definition.NumericId) ? player.skillOrder[(SkillType)definition.NumericId] : 0,
                    ContentCategory.Affliction => player.injuryOrder.ContainsKey((InjuryType)definition.NumericId) ? player.injuryOrder[(InjuryType)definition.NumericId] : 0,
                    _ => 0
                };
                state.Ownership.Add(definition.Key, new OwnedContentState(definition.Category, definition.NumericId, level, order));
            }
            state.SetFeature("foundation:battle-key", State.BattleKey);
            envelope.Players.Add(checkpointPlayer.playerID, state);
        }
        Captured.Add(SnapshotKey(identity), envelope.Clone());
        CaptureFailures.Remove(identity.RunKey);
    }

    private static PlayerContentState Clone(PlayerContentState value) =>
        System.Text.Json.JsonSerializer.Deserialize<PlayerContentState>(System.Text.Json.JsonSerializer.Serialize(value));
    private static int Owned(PlayerData player, ContentDescriptor definition) => definition.Category switch
    {
        ContentCategory.Skill => player.HasSkill((SkillType)definition.NumericId),
        ContentCategory.Affliction => player.HasInjuryInData((InjuryType)definition.NumericId),
        ContentCategory.Equipment => player.HasEquipment((PurchaseType)definition.NumericId) || player.previousEquipment == (PurchaseType)definition.NumericId ? 1 : 0,
        ContentCategory.Surgery => player.previousSurgeries.Contains((SurgeryType)definition.NumericId) ? 1 : 0,
        _ => 0
    };

    private static bool WriteBefore(string fileName, Il2CppStructArray<byte> data, IPlatform.Sku sku, ref NativeTask __result, out WriteTicket __state)
    {
        __state = null;
        if (fileName != "SaveData" || sku != IPlatform.Sku.DEFAULT || data is null) return true;
        try
        {
            var payload = Encoding.UTF8.GetString(data.ToArray());
            var ticket = new WriteTicket();
            foreach (var identity in CheckpointJson.FromSave(payload, _noneIndex).Where(value => value.Mode == "Solo"))
            {
                if (CaptureFailures.Contains(identity.RunKey)) throw new InvalidDataException("Feature state capture failed for this native run checkpoint.");
                if (Captured.TryGetValue(SnapshotKey(identity), out var snapshot)) _store.Prepare(snapshot);
                if (_store.HasPrepared(identity)) ticket.Identities.Add(identity);
                else if (identity.Token > 0)
                    throw new InvalidDataException("Marked native checkpoint has no matching prepared custom snapshot.");
            }
            __state = ticket;
            return true;
        }
        catch (Exception exception)
        {
            FoundationServices.Logger?.LogError("[Foundation] SaveData write BLOCKED because its matching custom snapshot could not be prepared; previous native file preserved: " + exception);
            __result = NativeTask.FromException(new Il2CppSystem.InvalidOperationException("Custom checkpoint preparation failed; previous save preserved."));
            return false;
        }
    }
    private static void WriteAfter(NativeTask __result, WriteTicket __state)
    {
        if (__state is null || __result is null || __state.Identities.Count == 0) return;
        if (Writes.Any(value => value.Task.Pointer == __result.Pointer)) return;
        __state.Task = __result;
        Writes.Add(__state);
        Tick();
    }
    internal static void Tick()
    {
        foreach (var ticket in Writes.ToArray())
        {
            try
            {
                if (!ticket.Task.IsCompleted) continue;
                Writes.Remove(ticket);
                var succeeded = !ticket.Task.IsFaulted && !ticket.Task.IsCanceled;
                foreach (var identity in ticket.Identities) _store.Commit(identity, succeeded);
                if (!succeeded) FoundationServices.Logger?.LogError("[Foundation] native SaveData task failed; custom snapshots remain uncommitted");
                else FoundationServices.Logger?.LogInfo("[Foundation] matching custom checkpoint snapshots committed after native SaveData success");
            }
            catch (Exception exception)
            {
                Writes.Remove(ticket);
                FoundationServices.Logger?.LogError("[Foundation] custom checkpoint commit failed; pending snapshot preserved: " + exception);
            }
        }
    }

    private static CheckpointEnvelope Load(RunDataManager.Checkpoint checkpoint)
    {
        var identity = Identity(checkpoint);
        if (Captured.TryGetValue(SnapshotKey(identity), out var captured)) return captured.Clone();
        if (identity.Token == 0)
        {
            if (_store.HasPrepared(identity))
            {
                var provenLegacy = File.Exists(_savePath) && CheckpointJson.FromSave(File.ReadAllText(_savePath), _noneIndex)
                    .Any(value => value.NativeFingerprint == identity.NativeFingerprint && value.RunKey == identity.RunKey);
                return _store.Read(identity, provenLegacy);
            }
            // Legacy checkpoints contain no external feature state. Import only
            // explicitly registered legacy markers; never call an acquisition API.
            var legacy = new CheckpointEnvelope { Identity = identity, LegacyImported = true };
            for (var playerIndex = 0; playerIndex < checkpoint.playerCount; playerIndex++)
            {
                var native = checkpoint.playerDatas[playerIndex];
                var state = new PlayerContentState();
                foreach (var reserved in new[] { (ContentCategory.Skill, (int)SkillType.None),
                    (ContentCategory.Affliction, (int)InjuryType.EmptySlot1) })
                {
                    var levels = reserved.Item1 == ContentCategory.Skill ? native.skillLevels : native.injuryLevels;
                    if (levels[NativeIndex(reserved.Item1, reserved.Item2)] != 0 &&
                        !FoundationServices.Content.Entries.Any(value => value.Legacy?.ArrayCategory == reserved.Item1 &&
                            value.Legacy.NativeMarkerId == reserved.Item2))
                        throw new InvalidDataException("Legacy Foundation marker has no migration owner. Load the compatible original content pack for its first import.");
                }
                foreach (var definition in FoundationServices.Content.Entries.Where(value => value.Legacy is not null))
                {
                    var markerIndex = NativeIndex(definition.Legacy.ArrayCategory, definition.Legacy.NativeMarkerId);
                    var levels = definition.Legacy.ArrayCategory == ContentCategory.Skill ? native.skillLevels : native.injuryLevels;
                    var orders = definition.Legacy.ArrayCategory == ContentCategory.Skill ? native.skillOrder : native.injuryOrder;
                    var level = levels[markerIndex];
                    if (level < 0 || level > definition.MaximumLevel) throw new InvalidDataException("Legacy marker conflicts with its registered meaning.");
                    StateProjection.ImportLegacy(state, definition.Key, definition, level, orders[markerIndex]);
                }
                legacy.Players.Add(native.playerID, state);
            }
            // A legacy import can itself be saved only when the exact native
            // checkpoint is successfully written, using the same prepare/commit path.
            Captured.TryAdd(SnapshotKey(identity), legacy.Clone());
            return legacy;
        }
        var proof = File.Exists(_savePath) && CheckpointJson.FromSave(File.ReadAllText(_savePath), _noneIndex)
            .Any(value => value.Mode == identity.Mode && value.RunKey == identity.RunKey && value.Token == identity.Token && value.NativeFingerprint == identity.NativeFingerprint);
        return _store.Read(identity, proof);
    }

    private static bool AllowRestore(NetworkSession __instance)
    {
        if (!SessionAccess.IsSoloMode || !__instance.IsServer) return true;
        try
        {
            var checkpoint = __instance.LastCheckpoint?.runData;
            if (checkpoint is not null)
            {
                var envelope = Load(checkpoint);
                foreach (var state in envelope.Players.Values) StateProjection.RequireAvailableOwnership(state, FoundationServices.Content);
                FoundationServices.ValidateFeatures(envelope);
            }
            return true;
        }
        catch (Exception exception)
        {
            FoundationServices.Logger?.LogError("[Foundation] checkpoint restore BLOCKED before native restore: " + exception +
                " Restore the matching SaveData and ModFoundation/checkpoints backup together.");
            return false;
        }
    }
    private static void RestoreBefore(RunDataManager.Checkpoint checkpoint)
    {
        if (!SessionAccess.IsSoloMode) return;
        _restoring = Load(checkpoint);
        FoundationServices.ValidateFeatures(_restoring);
        foreach (var state in _restoring.Players.Values) StateProjection.RequireAvailableOwnership(state, FoundationServices.Content);
        State.Restore(_restoring);
        FoundationServices.PublishReset(); // Clear transient charges; ownership restore is assignment, never acquisition.
    }
    private static void RestoreAfter()
    {
        if (_restoring is not null) FoundationServices.PublishRestore(_restoring.Identity.NativeFingerprint);
    }
    private static Exception RestoreFinally(Exception __exception) { _restoring = null; return __exception; }

    private static void PlayerRestoreBefore(PlayerData.Checkpoint checkpoint, out PlayerRestoreScope __state)
    {
        __state = new PlayerRestoreScope();
        if (SessionAccess.IsSoloMode)
        {
            var snapshot = _restoring;
            if (snapshot is null)
            {
                // The native character-preview path restores PlayerData directly.
                // Only use the saved root if this player checkpoint is an exact match.
                var root = PersistentData.Current?.CheckpointSolo?.runData;
                if (root?.playerDatas is not null)
                {
                    var wanted = CheckpointJson.Fingerprint(NativeJson.SerializeObject(checkpoint));
                    for (var index = 0; index < root.playerCount; index++)
                        if (CheckpointJson.Fingerprint(NativeJson.SerializeObject(root.playerDatas[index])) == wanted)
                        { snapshot = Load(root); break; }
                }
            }
            if (snapshot is not null) snapshot.Players.TryGetValue(checkpoint.playerID, out __state.State);
        }
        __state.Markers = Hide(checkpoint);
    }
    private static void PlayerRestoreAfter(PlayerData __instance, PlayerRestoreScope __state)
    {
        if (__state?.State is null) return;
        foreach (var entry in StateProjection.Active(__state.State, FoundationServices.Content))
        {
            if (entry.Value.Category == ContentCategory.Skill)
            {
                Set(__instance.skills, (SkillType)entry.Value.NumericId, entry.Value.Level);
                Set(__instance.skillOrder, (SkillType)entry.Value.NumericId, entry.Value.Order);
            }
            else if (entry.Value.Category == ContentCategory.Affliction)
            {
                Set(__instance.injuries, (InjuryType)entry.Value.NumericId, entry.Value.Level);
                Set(__instance.injuryOrder, (InjuryType)entry.Value.NumericId, entry.Value.Order);
            }
            // Native equipment and surgery histories remain authoritative.
        }
    }
    private static void Set<T>(Il2CppSystem.Collections.Generic.Dictionary<T, int> values, T key, int value)
    { if (values.ContainsKey(key)) values[key] = value; else values.Add(key, value); }
    private static Exception PlayerRestoreFinally(Exception __exception, PlayerRestoreScope __state)
    { RestoreMarkers(__state?.Markers); return __exception; }
    private static void ValidateBefore(PlayerData.Checkpoint __instance, out MarkerScope __state) => __state = Hide(__instance);
    private static void SyncBefore(PlayerData.Checkpoint checkpoint, out MarkerScope __state) => __state = Hide(checkpoint);
    private static Exception ValidateFinally(Exception __exception, MarkerScope __state)
    { RestoreMarkers(__state); return __exception; }

    private static MarkerScope Hide(PlayerData.Checkpoint checkpoint)
    {
        var scope = new MarkerScope();
        if (checkpoint is null) return scope;
        void HideValue(ContentCategory category, int index)
        {
            if (scope.Values.Any(value => value.Category == category && value.Index == index)) return;
            var levels = category == ContentCategory.Skill ? checkpoint.skillLevels : checkpoint.injuryLevels;
            var orders = category == ContentCategory.Skill ? checkpoint.skillOrder : checkpoint.injuryOrder;
            if (levels is null || orders is null || index < 0 || index >= levels.Length || index >= orders.Length || levels[index] == 0) return;
            scope.Values.Add((checkpoint, category, index, levels[index], orders[index]));
            levels[index] = 0; orders[index] = 0;
        }
        if (checkpoint.skillLevels is not null && _noneIndex < checkpoint.skillLevels.Length && checkpoint.skillLevels[_noneIndex] == CheckpointEnvelope.Marker)
            HideValue(ContentCategory.Skill, _noneIndex);
        foreach (var definition in FoundationServices.Content.Entries.Where(value => value.Legacy is not null))
            HideValue(definition.Legacy.ArrayCategory, NativeIndex(definition.Legacy.ArrayCategory, definition.Legacy.NativeMarkerId));
        return scope;
    }
    private static void RestoreMarkers(MarkerScope scope)
    {
        if (scope is null) return;
        foreach (var value in scope.Values)
        {
            var levels = value.Category == ContentCategory.Skill ? value.Checkpoint.skillLevels : value.Checkpoint.injuryLevels;
            var orders = value.Category == ContentCategory.Skill ? value.Checkpoint.skillOrder : value.Checkpoint.injuryOrder;
            if (levels is not null && orders is not null && value.Index < levels.Length && value.Index < orders.Length)
            { levels[value.Index] = value.Level; orders[value.Index] = value.Order; }
        }
        scope.Values.Clear();
    }
    private static int NativeIndex(ContentCategory category, int nativeId)
    {
        var type = category == ContentCategory.Skill ? typeof(SkillType) : typeof(InjuryType);
        var index = 0;
        foreach (var value in Enum.GetValues(type)) { if (Convert.ToInt32(value) == nativeId) return index; index++; }
        throw new InvalidOperationException("Native marker enum value is absent.");
    }

    private static void Reset()
    {
        State.Clear();
        Captured.Clear();
        CaptureFailures.Clear();
        NativeOwnership.Reset();
        FoundationServices.PublishReset();
        // Committed/pending snapshots are immutable archives for older save
        // rollback. They are never selected as current state by a run seed alone.
    }
    private static void Cleared(NetworkSession __instance)
    {
        if (SessionAccess.IsSoloMode && __instance.IsServer && __instance.LastCheckpoint is null) Reset();
    }
    private static void NewRun(RunDataManager __instance)
    {
        if (SessionAccess.IsSoloMode && __instance.runStartTime != 0) EnsureRun(__instance);
    }
    private static void LevelStateChanged(GameLevel __instance, int stateIdx)
    {
        if (stateIdx != (int)GameLevel.eSceneState.RUNNING || !SessionAccess.IsSoloMode || !NetworkSession.InstanceExists) return;
        var manager = NetworkSession.Instance.runDataManager;
        EnsureRun(manager);
        if (State.EnterBattle(manager.CompletedLevels)) FoundationServices.PublishBattle(State.BattleKey);
    }
}

public sealed class FoundationRuntimeController : MonoBehaviour
{
    public FoundationRuntimeController(IntPtr pointer) : base(pointer) { }
    public void Update() { NativePersistence.Tick(); NativeCombat.Tick(); NativeThrowing.Tick(); FoundationServices.PublishTick(); }
    public void OnApplicationQuit() => NativePersistence.Tick();
}
