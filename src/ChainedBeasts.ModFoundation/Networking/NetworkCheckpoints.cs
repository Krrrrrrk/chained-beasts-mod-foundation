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
    private static readonly Dictionary<ulong, (string Run, double At)> Snapshots = new();
    private static readonly HashSet<ulong> Awaiting = new();
    private static GameLobby _day;
    private static NetworkSession _restore;
    private static string _nonce, _prepare;
    private static double _deadline, _nextOwner;
    private static long _outgoing;
    private static bool _dayPass, _restorePass;
    private static bool _ownerSuspended;
    private static string _restoreNonce;
    internal static string Epoch { get; private set; } = "initial";
    internal static void Install(Harmony harmony)
    {
        FeaturePatcher.Patch(harmony, typeof(NetworkCheckpoints), FeaturePatcher.RequireMethod(typeof(GameLobby), nameof(GameLobby.DayTransitionRpc)), prefix: nameof(DayBefore));
        NativeMultiplayer.Message += Receive;
        FoundationServices.RunReset += Reset;
    }
    private static bool DayBefore(GameLobby __instance)
    {
        if (!NativeMultiplayer.Online || !__instance.IsServer || __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute || _dayPass) return true;
        if (!NativeMultiplayer.Ready) return false;
        if (NativeMultiplayer.Peers.Length == 0) return true;
        if (_day is null)
        {
            NativePersistence.ExportOwnedFeatures(); _day = __instance;
            Begin("prepare");
        }
        return false;
    }
    private static void Begin(string kind, CheckpointEnvelope envelope = null)
    {
        _nonce = Guid.NewGuid().ToString("N"); _deadline = Time.unscaledTimeAsDouble + 15;
        Awaiting.Clear(); foreach (var id in NativeMultiplayer.Peers) Awaiting.Add(id);
        NativeMultiplayer.Broadcast(new() { Kind = kind, Nonce = _nonce, Run = NativePersistence.RunKey,
            Json = envelope is null ? null : JsonSerializer.Serialize(envelope) });
    }
    internal static bool AllowRestore(NetworkSession session, CheckpointEnvelope envelope)
    {
        if (_restorePass || NativeMultiplayer.Peers.Length == 0) return true;
        if (_restore is null) { _restore = session; Begin("restore", envelope); Epoch = _nonce; }
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
    internal static void Publish(CheckpointEnvelope envelope) => NativeMultiplayer.Broadcast(new() { Kind = "checkpoint", Json = JsonSerializer.Serialize(envelope) });
    private static void Receive(ulong sender, MultiplayerPacket packet)
    {
        if (MultiplayerServices.IsHost)
        {
            if (packet.Kind == "restore-ack" && _restore is not null && packet.Nonce == _nonce) { Awaiting.Remove(sender); return; }
            if (packet.Kind != "owner-state" || packet.Run != NativePersistence.RunKey + "/" + Epoch || !SnapshotSequence.Accept(sender.ToString(), packet.Sequence)) return;
            var states = JsonSerializer.Deserialize<Dictionary<int, PlayerContentState>>(packet.Json);
            if (states is null || states.Count is < 1 or > 4) return;
            foreach (var pair in states)
            {
                var client = NetworkSession.Instance.GetClientByPlayerIndex((ulong)pair.Key);
                if (client is null || client.NetworkID != sender) throw new InvalidOperationException("Remote player state has the wrong owner.");
            }
            foreach (var pair in states) NativePersistence.ImportOwnerFeatures(pair.Key, pair.Value);
            Snapshots[sender] = (NativePersistence.RunKey, Time.unscaledTimeAsDouble);
            if (packet.Nonce == _nonce) Awaiting.Remove(sender);
            return;
        }
        if (packet.Kind == "prepare") { _prepare = packet.Nonce; _nextOwner = 0; return; }
        if (packet.Kind == "restore-cancel" && packet.Nonce == _restoreNonce) { _ownerSuspended = false; _restoreNonce = null; return; }
        if (packet.Kind is "checkpoint" or "restore")
        {
            var envelope = JsonSerializer.Deserialize<CheckpointEnvelope>(packet.Json);
            NativePersistence.AcceptEnvelope(envelope, packet.Kind != "restore");
            if (packet.Kind == "restore") { Epoch = packet.Nonce; _ownerSuspended = true; _restoreNonce = packet.Nonce; NativeMultiplayer.Send(NetworkManager.ServerClientId, new() { Kind = "restore-ack", Nonce = packet.Nonce }); }
        }
    }
    internal static void Tick()
    {
        if (!NativeMultiplayer.Ready || !NetworkSession.InstanceExists || NetworkSession.Instance.runDataManager is not { } manager) return;
        var now = Time.unscaledTimeAsDouble;
        if (!MultiplayerServices.IsHost && manager.runStartTime != 0 && !_ownerSuspended && now >= _nextOwner)
        {
            _nextOwner = now + .5;
            try
            {
                var states = NativePersistence.ExportOwnedFeatures();
                if (states.Count > 0) NativeMultiplayer.Send(NetworkManager.ServerClientId, new() { Kind = "owner-state", Sequence = ++_outgoing,
                    Nonce = _prepare, Run = NativePersistence.RunKey + "/" + Epoch, Json = JsonSerializer.Serialize(states) });
                _prepare = null;
            }
            catch (InvalidOperationException) { /* Wait for authoritative HP receipts before checkpointing. */ }
        }
        if (!MultiplayerServices.IsHost || (_day is null && _restore is null)) return;
        if (now > _deadline)
        {
            FoundationServices.Logger?.LogError("[Multiplayer] checkpoint agreement timed out; transition/restore cancelled and previous save preserved");
            if (_restore is not null)
            {
                NativeMultiplayer.Broadcast(new() { Kind = "restore-cancel", Nonce = _nonce });
                foreach (var peer in Awaiting.ToArray()) NativeMultiplayer.DisconnectPeer(peer, "Custom checkpoint restore could not be agreed. Rejoin the lobby before resuming.");
            }
            _day = null; _restore = null; Awaiting.Clear(); return;
        }
        if (Awaiting.Count != 0) return;
        if (_day is not null)
        {
            var day = _day; _day = null; _dayPass = true;
            try { NativePersistence.ExportOwnedFeatures(); day.DayTransitionRpc(); }
            finally { _dayPass = false; }
        }
        if (_restore is not null)
        {
            var session = _restore; _restore = null; _restorePass = true;
            try { session.RestoreCheckpoint(); }
            finally { _restorePass = false; }
        }
    }
    internal static void Reset() { SnapshotSequence.Clear(); Snapshots.Clear(); Awaiting.Clear(); _day = null; _restore = null; _nonce = _prepare = null; _nextOwner = 0; _outgoing = 0; }
    internal static void RestoreApplied() { _ownerSuspended = false; _restoreNonce = null; }
    internal static void ResetSession() { Reset(); Epoch = "initial"; _ownerSuspended = false; _restoreNonce = null; }
}
