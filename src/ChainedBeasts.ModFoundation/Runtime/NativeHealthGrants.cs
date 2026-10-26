using System;
using System.Collections.Generic;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.Networking;
using Unity.Netcode;

namespace ChainedBeasts.ModFoundation.Runtime;

/// <summary>Owner requests, server-native healing and an actual-HP receipt before cosmetic acknowledgement.</summary>
internal static class NativeHealthGrants
{
    private sealed class Grant
    {
        internal Player Player;
        internal string Key, Run;
        internal ulong Owner;
        internal long Sequence;
        internal bool NativeCaptured;
        internal readonly ResourceGrantReceipt Receipt = new();
    }
    internal sealed class HealScope
    {
        private readonly Grant _grant;
        private readonly int _before;
        private HealScope(Grant grant, int before) { _grant = grant; _before = before; }
        internal void Complete(Player player)
        {
            if (!_grant.Receipt.TryCommit(_before, player.health.Value, out var actual)) return;
            if (player.IsOwner) CombatServices.NotifyActivation(player, _grant.Key, actual);
            else NativeMultiplayer.Send(_grant.Owner, new() { Kind = "heal-feedback", Player = player.PlayerData.playerID,
                Json = _grant.Key, Run = _grant.Run, Sequence = _grant.Sequence, Amount = actual });
        }
        internal static HealScope Capture(NetworkHealthObject target)
        {
            if (_active is not { } grant || grant.NativeCaptured || target?.IsServer != true ||
                target.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Execute || grant.Player.Pointer != target.Pointer) return null;
            grant.NativeCaptured = true;
            return new(grant, Math.Max(0, target.health.Value));
        }
    }
    [ThreadStatic] private static Grant _active;
    private static readonly SequenceWindow Requests = new(), Feedback = new();
    private static readonly HashSet<string> Kills = new(StringComparer.Ordinal);
    private static bool _feedbackErrorReported;
    private static long _sequence;
    private static string Scope => NativePersistence.RunKey + "/" + NativePersistence.BattleKey + "/" + NetworkCheckpoints.Epoch;
    internal static void Initialize()
    {
        NativeMultiplayer.Message += Receive;
        FoundationServices.RunReset += ResetSession;
        FoundationServices.BattleStarted += _ => Kills.Clear();
    }
    internal static void Request(Player player, string key, int amount)
    {
        if (!NativeCombat.Active || player is null || !player.IsOwner || !CombatServices.ConfirmedAlive(player) || amount <= 0) return;
        var packet = new MultiplayerPacket { Kind = "heal-grant", Player = player.PlayerData.playerID, Json = key,
            Amount = amount, Sequence = ++_sequence, Nonce = NetworkCombat.CurrentReceipt, Run = Scope };
        if (SessionAccess.IsSoloMode || MultiplayerServices.IsHost) Apply(player.NetworkManager.LocalClientId, packet);
        else NativeMultiplayer.Send(NetworkManager.ServerClientId, packet);
    }
    private static void Receive(ulong sender, MultiplayerPacket packet)
    {
        if (packet.Run != Scope || !NativeMultiplayer.Ready) return;
        if (packet.Kind == "heal-grant" && MultiplayerServices.IsHost) { Apply(sender, packet); return; }
        if (packet.Kind != "heal-feedback" || MultiplayerServices.IsHost || packet.Amount <= 0 ||
            !MultiplayerServices.HealthRules.ContainsKey(packet.Json) || !Feedback.Accept("feedback", packet.Sequence)) return;
        var player = NativePersistence.PlayerById(NetworkSession.Instance.runDataManager, packet.Player)?.PlayerObject;
        if (player?.IsOwner == true) CombatServices.NotifyActivation(player, packet.Json, packet.Amount);
    }
    private static void Apply(ulong sender, MultiplayerPacket packet)
    {
        if (!SessionAccess.GameplayReady || !MultiplayerServices.IsHost || packet.Run != Scope ||
            !MultiplayerServices.HealthRules.TryGetValue(packet.Json, out var rule) ||
            !FoundationServices.Content.TryGet(packet.Json, out var content) || !content.Enabled) return;
        var data = NativePersistence.PlayerById(NetworkSession.Instance.runDataManager, packet.Player);
        var player = data?.PlayerObject;
        if (player is null || player.OwnerClientId != sender || player.IsDead || player.health.Value <= 0 ||
            packet.Amount <= 0 || packet.Amount > ResourceGrantReceipt.MaximumRequest(player.MaxHealth, rule.MaximumFraction)) return;
        var owned = content.Category switch
        {
            ContentCategory.Equipment => data.currentEquipment == (PurchaseType)content.NumericId,
            ContentCategory.Surgery => data.previousSurgeries.Contains((SurgeryType)content.NumericId),
            _ => false
        };
        if (!owned || (NativeMultiplayer.Online && rule.RequiresKill && !NetworkCombat.VerifyKill(sender, packet.Player, packet.Nonce))) return;
        if (!Requests.Accept(sender.ToString(), packet.Sequence)) return;
        if (NativeMultiplayer.Online && rule.RequiresKill && (Kills.Count >= 4096 || !Kills.Add(packet.Player + "/" + packet.Json + "/" + packet.Nonce))) return;
        var prior = _active;
        _active = new() { Player = player, Key = packet.Json, Run = packet.Run, Owner = sender, Sequence = packet.Sequence };
        try { player.HealRpc(packet.Amount); } // Native cap, Marrow hook, HP variable and normal HUD path.
        finally { _active = prior; }
    }
    internal static HealScope Before(NetworkHealthObject target) => HealScope.Capture(target);
    internal static void After(NetworkHealthObject target, HealScope state)
    {
        if (state is null) return;
        try { state.Complete(target.Cast<Player>()); }
        catch (Exception exception)
        {
            // HP has already committed. Cosmetic delivery must never fail the native healing RPC or replay it.
            if (_feedbackErrorReported) return;
            _feedbackErrorReported = true;
            FoundationServices.Logger?.LogWarning("[Foundation] custom healing committed; activation acknowledgement unavailable: " + exception.Message);
        }
    }
    internal static void ResetSession() { Requests.Clear(); Feedback.Clear(); Kills.Clear(); _sequence = 0; _active = null; }
}
