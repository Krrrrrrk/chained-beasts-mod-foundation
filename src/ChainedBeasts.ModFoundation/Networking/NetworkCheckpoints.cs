using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.State;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace ChainedBeasts.ModFoundation.Networking;

/// <summary>Owner feature snapshots and host checkpoint envelopes; never sends native save files.</summary>
internal static class NetworkCheckpoints
{
    private static readonly SequenceWindow SnapshotSequence = new();
    private static readonly SequenceWindow CheckpointSequence = new();
    private static readonly Dictionary<ulong, (string Run, double At)> Snapshots = new();
    private static readonly HashSet<ulong> Awaiting = new();
    private static GameLobby _day;
    private static NetworkSession _restore;
    private static string _nonce, _prepare, _prepareRun;
    private static double _deadline, _nextOwner;
    private static long _outgoing;
    private static long _checkpointOutgoing;
    private static bool _dayPass, _restorePass;
    private static bool _ownerSuspended;
    private static string _restoreNonce;
    private static string _lastRestoreNonce;
    private static string _checkpointNonce;
    private static double _nativeRestoreDeadline;
    private static string _lastOwnerProblem;
    private static string _runScope;
    internal static string Epoch { get; private set; } = "initial";
    internal static void Install(Harmony harmony)
    {
        FeaturePatcher.Patch(harmony, typeof(NetworkCheckpoints), FeaturePatcher.RequireMethod(typeof(GameLobby), nameof(GameLobby.DayTransitionRpc)), prefix: nameof(DayBefore));
        NativeMultiplayer.Message += Receive;
        // RunReset is also published inside feature capture and native restore.
        // Transport agreements must survive those advisory feature resets.
    }
    private static bool DayBefore(GameLobby __instance)
    {
        if (!NativeMultiplayer.Online || !__instance.IsServer || __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute || _dayPass) return true;
        if (!NativeMultiplayer.Ready) return false;
        if (NativeMultiplayer.Peers.Length == 0) return true;
        if (_day is null)
        {
            // HP confirmations can legitimately still be in flight at round end.
            // Queue the native call before capture, so a transient wait cannot
            // escape through the native trampoline and lose the only callback.
            _day = __instance; _nonce = null; Awaiting.Clear(); _deadline = Time.unscaledTimeAsDouble + 15;
            try { NativePersistence.ExportOwnedFeatures(); Begin("prepare"); }
            catch (Exception exception) { OwnerProblem("Host checkpoint capture is waiting: " + exception.GetType().Name + ": " + exception.Message); }
        }
        return false;
    }
    private static void Begin(string kind, CheckpointEnvelope envelope = null, string wireFingerprint = null)
    {
        _nonce = Guid.NewGuid().ToString("N"); _deadline = Time.unscaledTimeAsDouble + 15;
        if (kind == "restore")
        {
            _checkpointNonce = null;
            Epoch = _nonce; _restoreNonce = _lastRestoreNonce = _nonce;
            _nativeRestoreDeadline = Time.unscaledTimeAsDouble + 45;
            NativePersistence.AuthorizeWireTransfer(envelope, wireFingerprint, Epoch, _nonce);
        }
        Awaiting.Clear(); foreach (var id in NativeMultiplayer.Peers) Awaiting.Add(id);
        var run = envelope?.Identity.RunKey ?? NativePersistence.RunKey;
        NativeMultiplayer.Broadcast(new() { Kind = kind, Nonce = _nonce, Run = run + "/" + Epoch, Fingerprint = wireFingerprint,
            Json = envelope is null ? null : JsonSerializer.Serialize(envelope) });
        FoundationServices.Logger?.LogInfo($"[Multiplayer] {kind} checkpoint agreement started; waiting for {Awaiting.Count} peer(s), scope={run}/{Epoch}");
    }
    internal static bool AllowRestore(NetworkSession session, CheckpointEnvelope envelope, string wireFingerprint)
    {
        if (_restorePass) return true;
        if (NativeMultiplayer.Peers.Length == 0)
        {
            NativePersistence.AuthorizeWireTransfer(envelope, wireFingerprint, Epoch, Guid.NewGuid().ToString("N"));
            return true;
        }
        if (_restore is null) { _restore = session; Begin("restore", envelope, wireFingerprint); }
        return false;
    }
    internal static void RequireOwners(RunDataManager manager)
    {
        if (!NativeMultiplayer.Online) return;
        for (var i = 0; i < manager.playerDatas.Count; i++)
        {
            var data = manager.playerDatas[i]; if (MultiplayerServices.IsPlayerOwner(data)) continue;
            var client = NetworkSession.Instance.GetClientByPlayerIndex((ulong)data.playerID);
            var hasOwned = FoundationServices.Content.Entries.Any(entry => entry.Enabled &&
                (entry.Category == Content.ContentCategory.Skill && data.HasSkill((SkillType)entry.NumericId) > 0 ||
                 entry.Category == Content.ContentCategory.Affliction && data.HasInjuryInData((InjuryType)entry.NumericId) > 0 ||
                 entry.Category == Content.ContentCategory.Equipment && data.currentEquipment == (PurchaseType)entry.NumericId ||
                 entry.Category == Content.ContentCategory.Surgery && data.previousSurgeries.Contains((SurgeryType)entry.NumericId)));
            if (hasOwned && (client is null || !Snapshots.TryGetValue(client.NetworkID, out var stamp) || stamp.Run != NativePersistence.RunKey || Time.unscaledTimeAsDouble - stamp.At > 3))
                throw new InvalidOperationException("Custom checkpoint awaits fresh settled owner state; prior save preserved.");
        }
    }
    internal static void Publish(CheckpointEnvelope envelope, string wireFingerprint)
    {
        var generation = Guid.NewGuid().ToString("N");
        _checkpointNonce = generation; _lastRestoreNonce = null;
        NativePersistence.AuthorizeWireTransfer(envelope, wireFingerprint, Epoch, generation);
        NativeMultiplayer.Broadcast(new() { Kind = "checkpoint", Sequence = ++_checkpointOutgoing,
            Nonce = generation, Run = envelope.Identity.RunKey + "/" + Epoch, Fingerprint = wireFingerprint, Json = JsonSerializer.Serialize(envelope) });
    }
    private static void Receive(ulong sender, MultiplayerPacket packet)
    {
        if (MultiplayerServices.IsHost)
        {
            if (packet.Kind == "checkpoint-failed" && packet.Nonce is not null && packet.Nonce == _checkpointNonce)
            { Abort("A peer rejected the current checkpoint transfer."); return; }
            if (packet.Kind == "restore-failed" && packet.Nonce is not null && (packet.Nonce == _restoreNonce || packet.Nonce == _lastRestoreNonce))
            { Abort("A peer could not apply the agreed native checkpoint."); return; }
            if (packet.Kind == "restore-ack" && _restore is not null && packet.Nonce == _nonce) { Awaiting.Remove(sender); return; }
            if (packet.Kind != "owner-state") return;
            if (packet.Run != NativePersistence.RunKey + "/" + Epoch)
            { OwnerProblem("Owner checkpoint scope differs: received " + packet.Run + "; expected " + NativePersistence.RunKey + "/" + Epoch); return; }
            if (!SnapshotSequence.Accept(sender + "/" + packet.Run, packet.Sequence)) return;
            var states = JsonSerializer.Deserialize<Dictionary<int, PlayerContentState>>(packet.Json);
            if (states is null || states.Count > 4) return;
            foreach (var pair in states)
            {
                var client = NetworkSession.Instance.GetClientByPlayerIndex((ulong)pair.Key);
                if (client is null || client.NetworkID != sender) throw new InvalidOperationException("Remote player state has the wrong owner.");
            }
            var manager = NetworkSession.Instance.runDataManager;
            var expected = new HashSet<int>();
            for (var i = 0; i < manager.playerDatas.Count; i++)
            {
                var data = manager.playerDatas[i];
                if (NetworkSession.Instance.GetClientByPlayerIndex((ulong)data.playerID)?.NetworkID == sender) expected.Add(data.playerID);
            }
            if (!expected.SetEquals(states.Keys))
            { OwnerProblem("Owner checkpoint roster is incomplete; expected " + expected.Count + ", received " + states.Count); return; }
            foreach (var pair in states) NativePersistence.ImportOwnerFeatures(pair.Key, pair.Value);
            Snapshots[sender] = (NativePersistence.RunKey, Time.unscaledTimeAsDouble);
            if (packet.Nonce == _nonce) Awaiting.Remove(sender);
            return;
        }
        if (packet.Kind == "prepare") { _prepare = packet.Nonce; _prepareRun = packet.Run; _nextOwner = 0; _lastOwnerProblem = null; return; }
        if (packet.Kind == "restore-cancel" && (packet.Nonce == _restoreNonce || packet.Nonce == _prepare))
        { _ownerSuspended = false; _restoreNonce = _prepare = _prepareRun = null; _nativeRestoreDeadline = 0; return; }
        if (packet.Kind is "checkpoint" or "restore")
        {
            var envelope = JsonSerializer.Deserialize<CheckpointEnvelope>(packet.Json);
            if (envelope?.Identity is null) throw new InvalidOperationException("Missing checkpoint transfer envelope.");
            var epoch = packet.Kind == "restore" ? packet.Nonce : Epoch;
            if (packet.Run != envelope.Identity.RunKey + "/" + epoch) throw new InvalidOperationException("Checkpoint transfer scope differs from its envelope.");
            if (packet.Kind == "checkpoint" && !CheckpointSequence.Accept("checkpoint", packet.Sequence)) return;
            try
            {
                NativePersistence.AuthorizeWireTransfer(envelope, packet.Fingerprint, epoch, packet.Nonce);
                NativePersistence.AcceptEnvelope(envelope, packet.Kind != "restore");
                if (packet.Kind == "restore")
                {
                    _checkpointNonce = null;
                    Epoch = epoch; _ownerSuspended = true; _restoreNonce = _lastRestoreNonce = packet.Nonce;
                    _nativeRestoreDeadline = Time.unscaledTimeAsDouble + 45;
                    NativeMultiplayer.Send(NetworkManager.ServerClientId, new() { Kind = "restore-ack", Nonce = packet.Nonce });
                }
                else { _checkpointNonce = packet.Nonce; _lastRestoreNonce = null; }
            }
            catch (Exception exception)
            {
                NativeMultiplayer.Send(NetworkManager.ServerClientId, new() { Kind = packet.Kind == "restore" ? "restore-failed" : "checkpoint-failed", Nonce = packet.Nonce });
                _ownerSuspended = false; _restoreNonce = null; _nativeRestoreDeadline = 0;
                NativePersistence.ClearWireTransfer();
                FoundationServices.Logger?.LogError("[Multiplayer] checkpoint transfer rejected: " + exception);
                throw;
            }
        }
    }
    internal static void Tick()
    {
        if (!NativeMultiplayer.Ready || !NetworkSession.InstanceExists || NetworkSession.Instance.runDataManager is not { } manager) return;
        var now = Time.unscaledTimeAsDouble;
        if (_restoreNonce is not null && now > _nativeRestoreDeadline)
        { RestoreFailed(new TimeoutException("Native checkpoint restore did not finish after the agreed transfer.")); return; }
        if (!MultiplayerServices.IsHost && manager.runStartTime != 0 && !_ownerSuspended && now >= _nextOwner)
        {
            _nextOwner = now + .5;
            try
            {
                var prepare = _prepare; var prepareRun = _prepareRun;
                var states = NativePersistence.ExportOwnedFeatures();
                var scope = NativePersistence.RunKey + "/" + Epoch;
                if (prepare is not null && prepareRun != scope)
                { OwnerProblem("Waiting for local checkpoint scope " + prepareRun + "; current " + scope); return; }
                NativeMultiplayer.Send(NetworkManager.ServerClientId, new() { Kind = "owner-state", Sequence = ++_outgoing,
                    Nonce = prepare, Run = scope, Json = JsonSerializer.Serialize(states) });
                if (_prepare == prepare) _prepare = _prepareRun = null;
            }
            catch (Exception exception) { OwnerProblem("Owner checkpoint capture is waiting: " + exception.GetType().Name + ": " + exception.Message); }
        }
        if (!MultiplayerServices.IsHost || (_day is null && _restore is null)) return;
        if (now > _deadline)
        {
            Abort("Checkpoint agreement timed out waiting for " + Awaiting.Count + " peer(s); previous save preserved."); return;
        }
        if (_day is not null && _nonce is null)
        {
            try { NativePersistence.ExportOwnedFeatures(); Begin("prepare"); }
            catch (Exception exception) { OwnerProblem("Host checkpoint capture is waiting: " + exception.GetType().Name + ": " + exception.Message); }
            return;
        }
        if (Awaiting.Count != 0) return;
        if (_day is not null)
        {
            var day = _day; _day = null; _dayPass = true;
            try { NativePersistence.ExportOwnedFeatures(); day.DayTransitionRpc(); FoundationServices.Logger?.LogInfo("[Multiplayer] owner checkpoint agreement complete; native day transition released"); }
            catch (Exception exception) { Abort("Day transition failed: " + exception.Message); }
            finally { _dayPass = false; }
        }
        if (_restore is not null)
        {
            var session = _restore; _restore = null; _restorePass = true;
            try { session.RestoreCheckpoint(); }
            catch (Exception exception) { RestoreFailed(exception); }
            finally { _restorePass = false; }
        }
    }
    private static void OwnerProblem(string message)
    {
        if (_lastOwnerProblem == message) return;
        _lastOwnerProblem = message; FoundationServices.Logger?.LogWarning("[Multiplayer] " + message);
    }
    private static void Abort(string reason)
    {
        FoundationServices.Logger?.LogError("[Multiplayer] " + reason + " Checkpoint operation cancelled; return to the lobby before another attempt.");
        NativeMultiplayer.Broadcast(new() { Kind = "restore-cancel", Nonce = _restoreNonce ?? _nonce });
        foreach (var peer in NativeMultiplayer.Peers) NativeMultiplayer.DisconnectPeer(peer, "Custom checkpoint operation failed. Return to the lobby and rejoin before starting/resuming.");
        _day = null; _restore = null; Awaiting.Clear(); _nonce = _prepare = _prepareRun = null;
        _ownerSuspended = false; _restoreNonce = _lastRestoreNonce = _checkpointNonce = null; _nativeRestoreDeadline = 0;
    }
    internal static void RestoreFailed(Exception exception)
    {
        FoundationServices.Logger?.LogError("[Multiplayer] native checkpoint apply failed: " + exception.GetType().Name + ": " + exception.Message);
        if (MultiplayerServices.IsHost) Abort("Native checkpoint apply failed; previous save preserved.");
        else
        {
            var captured = _restoreNonce is null && _checkpointNonce is not null;
            NativeMultiplayer.Send(NetworkManager.ServerClientId, new() { Kind = captured ? "checkpoint-failed" : "restore-failed", Nonce = _restoreNonce ?? _checkpointNonce ?? _lastRestoreNonce });
            _ownerSuspended = false; _restoreNonce = null; _nativeRestoreDeadline = 0;
        }
    }
    internal static void RestoreApplied() { _ownerSuspended = false; _restoreNonce = null; _nativeRestoreDeadline = 0; }
    internal static void ResetRun(string incomingRun)
    {
        if (incomingRun is not null && incomingRun == _runScope) return; // RPC send/execute postfixes can both observe the same new run.
        // A prepare for the incoming native run may arrive before StartNewRunRpc
        // finishes locally. Preserve only that request; abandon old agreements.
        var keepPrepare = _prepare is not null && (incomingRun is null || _prepareRun?.StartsWith(incomingRun + "/", StringComparison.Ordinal) == true);
        var prepare = keepPrepare ? _prepare : null; var scope = keepPrepare ? _prepareRun : null;
        _runScope = incomingRun;
        _day = null; _restore = null; Awaiting.Clear(); Snapshots.Clear();
        _nonce = null; _ownerSuspended = false; _restoreNonce = _lastRestoreNonce = _checkpointNonce = null; _nativeRestoreDeadline = 0;
        _prepare = prepare; _prepareRun = scope; _nextOwner = 0; _lastOwnerProblem = null;
        Epoch = scope is not null ? scope[(scope.LastIndexOf('/') + 1)..] : "initial";
        if (incomingRun is null) NativePersistence.ClearWireTransfer(); else NativePersistence.RetainWireRun(incomingRun);
        // Sequence windows/counters are session-scoped and monotonically advance.
        // In-flight packets from the previous run cannot reset their replay guards.
    }
    internal static void ResetSession()
    {
        SnapshotSequence.Clear(); CheckpointSequence.Clear(); Snapshots.Clear(); Awaiting.Clear(); _day = null; _restore = null;
        _nonce = _prepare = _prepareRun = null; _nextOwner = 0; _outgoing = _checkpointOutgoing = 0;
        Epoch = "initial"; _ownerSuspended = false; _restoreNonce = _lastRestoreNonce = _checkpointNonce = null; _nativeRestoreDeadline = 0; _lastOwnerProblem = _runScope = null;
        NativePersistence.ClearWireTransfer();
    }
}
