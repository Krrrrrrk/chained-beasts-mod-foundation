using System;
using System.Collections.Generic;
using System.Linq;
using ChainedBeasts.ModFoundation.Runtime;
using Unity.Netcode;

namespace ChainedBeasts.ModFoundation.Networking;

internal static class NetworkCombat
{
    private static readonly Dictionary<string, long> Ordinals = new(StringComparer.Ordinal);
    private static readonly ConfirmationMailbox<MultiplayerPacket> Mailbox = new();
    private sealed class Proof
    {
        internal MultiplayerPacket Packet; internal int Attacker, Victim; internal ulong AttackerOwner, VictimOwner; internal string TargetToken;
        internal Proof(MultiplayerPacket packet, int attacker, ulong attackerOwner, int victim, ulong victimOwner, string targetToken)
        { Packet = packet; Attacker = attacker; AttackerOwner = attackerOwner; Victim = victim; VictimOwner = victimOwner; TargetToken = targetToken; }
    }
    private static readonly Dictionary<string, Proof> Proofs = new(StringComparer.Ordinal);
    private static readonly Queue<string> ProofOrder = new();
    [ThreadStatic] internal static string CurrentReceipt;
    [ThreadStatic] internal static MultiplayerPacket Current;
    internal static void Initialize() => NativeMultiplayer.Message += Received;
    internal static string Begin(ulong origin, ulong target, ulong source)
    {
        var route = MultiplayerCodec.Fingerprint(new[] { NativePersistence.RunKey + "/" + NativePersistence.BattleKey + "/" + NetworkCheckpoints.Epoch,
            origin + "/" + target + "/" + source });
        var ordinal = Ordinals.TryGetValue(route, out var prior) ? prior + 1 : 1;
        Ordinals[route] = ordinal; return route + "/" + ordinal;
    }
    internal static void Confirm(string key, NetworkHealthObject target, NetworkAgent source, ulong origin, int before, Action<MultiplayerPacket> callback)
    {
        var packet = new MultiplayerPacket { Kind = "hp", Nonce = key, Target = target.NetworkObjectId, Source = source?.NetworkObjectId ?? ulong.MaxValue,
            Origin = origin, Before = before, After = Math.Max(0, target.health.Value), Dead = target.IsDead,
            EnemySource = source?.TryCast<Enemy>() is not null && !source.IsPlayerTeam,
            EnemyTarget = target.TryCast<Enemy>() is { IsPlayerTeam: false } };
        var attacker = source?.TryCast<Player>(); var victim = target.TryCast<Player>();
        Proofs[key] = new(packet, attacker?.PlayerData?.playerID ?? -1, attacker?.OwnerClientId ?? ulong.MaxValue,
            victim?.PlayerData?.playerID ?? -1, victim?.OwnerClientId ?? ulong.MaxValue, NativeCombat.TargetIdentity(target));
        ProofOrder.Enqueue(key); while (ProofOrder.Count > 4096) Proofs.Remove(ProofOrder.Dequeue());
        var recipients = new HashSet<ulong> { origin, attacker?.OwnerClientId ?? ulong.MaxValue, victim?.OwnerClientId ?? ulong.MaxValue };
        foreach (var peer in NativeMultiplayer.Peers) if (recipients.Contains(peer)) NativeMultiplayer.Send(peer, packet);
        Await(key, callback); Mailbox.Confirm(key, packet);
    }
    internal static void Await(string key, Action<MultiplayerPacket> callback)
    {
        Mailbox.Wait(key, packet => Apply(key, packet, callback));
    }
    private static void Received(ulong sender, MultiplayerPacket packet)
    {
        if (packet.Kind != "hp" || MultiplayerServices.IsHost || string.IsNullOrEmpty(packet.Nonce) || packet.Before < 0 || packet.After < 0) return;
        Mailbox.Confirm(packet.Nonce, packet);
    }
    private static void Apply(string key, MultiplayerPacket packet, Action<MultiplayerPacket> callback)
    {
        var prior = CurrentReceipt; var current = Current; CurrentReceipt = key; Current = packet;
        try { callback(packet); } finally { CurrentReceipt = prior; Current = current; }
    }
    internal static bool VerifyGold(ulong sender, int player, string receipt, bool outgoing) => receipt is not null && Proofs.TryGetValue(receipt, out var proof) &&
        ReceiptAuthority.Allows(sender, player, outgoing, proof.Packet.Before, proof.Packet.After, proof.Attacker, proof.AttackerOwner,
            proof.Packet.EnemyTarget, proof.Victim, proof.VictimOwner, proof.Packet.EnemySource);
    internal static string ProofTarget(string key) => Proofs[key].TargetToken;
    internal static bool VerifyKill(ulong sender, int player, string receipt) => VerifyGold(sender, player, receipt, true) && Proofs[receipt].Packet.Dead;
    internal static void RequireSettled() { if (Mailbox.PendingCount != 0) throw new InvalidOperationException("Checkpoint awaits confirmed multiplayer HP receipts."); }
    internal static void Reset() { Ordinals.Clear(); Mailbox.Clear(); Proofs.Clear(); ProofOrder.Clear(); CurrentReceipt = null; Current = null; }
}
