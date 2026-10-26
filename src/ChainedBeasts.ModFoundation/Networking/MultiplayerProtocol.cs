using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChainedBeasts.ModFoundation.Networking;

/// <summary>Bounded, versioned wire data. Sender identity always comes from NGO, never JSON.</summary>
public sealed class MultiplayerPacket
{
    public int Protocol { get; set; } = 1;
    public string Kind { get; set; }
    public string Session { get; set; }
    public string Fingerprint { get; set; }
    public string Run { get; set; }
    public string Nonce { get; set; }
    public long Sequence { get; set; }
    public string Json { get; set; }
    public int Player { get; set; }
    public int Amount { get; set; }
    public ulong Target { get; set; }
    public ulong Source { get; set; }
    public ulong Origin { get; set; }
    public int Before { get; set; }
    public int After { get; set; }
    public bool EnemySource { get; set; }
    public bool EnemyTarget { get; set; }
    public bool Dead { get; set; }
}
public static class MultiplayerCodec
{
    public const int MaximumBytes = 60000;
    public static string Fingerprint(IEnumerable<string> values) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join("\n", values.OrderBy(x => x, StringComparer.Ordinal))))).ToLowerInvariant();
    public static byte[] Encode(MultiplayerPacket packet)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(packet);
        if (bytes.Length > MaximumBytes) throw new InvalidOperationException("Multiplayer packet exceeds its bounded size.");
        return bytes;
    }
    public static MultiplayerPacket Decode(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0 || bytes.Length > MaximumBytes) throw new InvalidOperationException("Invalid multiplayer packet size.");
        var packet = JsonSerializer.Deserialize<MultiplayerPacket>(bytes, new JsonSerializerOptions { MaxDepth = 16 });
        if (packet is null || packet.Protocol != 1 || packet.Kind is null || packet.Kind.Length > 32 ||
            packet.Session?.Length > 64 || packet.Nonce?.Length > 256 || packet.Run?.Length > 256)
            throw new InvalidOperationException("Invalid multiplayer protocol or identity.");
        return packet;
    }
}
/// <summary>Reliable-message replay window, scoped to a server session rather than a process lifetime.</summary>
public sealed class SequenceWindow
{
    private readonly Dictionary<string, long> _values = new(StringComparer.Ordinal);
    public bool Accept(string stream, long sequence)
    {
        if (string.IsNullOrEmpty(stream) || sequence <= 0) return false;
        if (_values.TryGetValue(stream, out var last) && sequence <= last) return false;
        if (_values.Count >= 2048 && !_values.ContainsKey(stream)) return false;
        _values[stream] = sequence; return true;
    }
    public void Clear() => _values.Clear();
}
public sealed class ReadyBarrier
{
    private readonly HashSet<ulong> _pending = new();
    public bool Complete => _pending.Count == 0;
    public void Begin(IEnumerable<ulong> peers) { _pending.Clear(); foreach (var peer in peers) _pending.Add(peer); }
    public bool Acknowledge(ulong sender) => _pending.Remove(sender);
    public void Retain(IEnumerable<ulong> peers) => _pending.IntersectWith(peers);
    public void Clear() => _pending.Clear();
}
public static class ReceiptAuthority
{
    public static bool Allows(ulong sender, int player, bool outgoing, int before, int after,
        int attacker, ulong attackerOwner, bool enemyTarget, int victim, ulong victimOwner, bool enemySource) =>
        player >= 0 && before > Math.Max(0, after) && before > 0 && (outgoing
            ? attacker == player && attackerOwner == sender && enemyTarget
            : victim == player && victimOwner == sender && enemySource);
}
public sealed class SharedMoneySnapshot
{
    public int Money { get; set; }
    public int Earned { get; set; }
    public int[] Ledger { get; set; } = Array.Empty<int>();
    public string Battle { get; set; }
    public bool IsValid() => Money >= 0 && Earned >= 0 && Ledger is not null && Ledger.Length <= 4 &&
        Ledger.All(id => id >= 0) && Ledger.Distinct().Count() == Ledger.Length && (Battle is null || Battle.Length <= 256);
}
/// <summary>Either native-RPC or confirmation may arrive first. One delivery per key, bounded recent history.</summary>
public sealed class ConfirmationMailbox<T>
{
    private readonly int _capacity;
    private readonly Dictionary<string, Action<T>> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, T> _early = new(StringComparer.Ordinal);
    private readonly Queue<string> _earlyOrder = new();
    private readonly HashSet<string> _delivered = new(StringComparer.Ordinal);
    private readonly Queue<string> _deliveryOrder = new();
    public int PendingCount => _pending.Count;
    public ConfirmationMailbox(int capacity = 4096) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); _capacity = capacity; }
    public void Wait(string key, Action<T> apply)
    {
        if (string.IsNullOrEmpty(key) || apply is null) throw new ArgumentException("Receipt requires identity and callback.");
        if (_delivered.Contains(key) || _pending.ContainsKey(key)) return;
        if (_early.Remove(key, out var value)) { Deliver(key, value, apply); return; }
        if (_pending.Count >= _capacity) throw new InvalidOperationException("Too many unconfirmed HP receipts.");
        _pending.Add(key, apply);
    }
    public void Confirm(string key, T value)
    {
        if (string.IsNullOrEmpty(key) || _delivered.Contains(key) || _early.ContainsKey(key)) return;
        if (_pending.Remove(key, out var apply)) { Deliver(key, value, apply); return; }
        _early[key] = value; _earlyOrder.Enqueue(key);
        while (_earlyOrder.Count > _capacity) _early.Remove(_earlyOrder.Dequeue());
    }
    private void Deliver(string key, T value, Action<T> apply)
    {
        if (!_delivered.Add(key)) return;
        _deliveryOrder.Enqueue(key);
        while (_deliveryOrder.Count > _capacity) _delivered.Remove(_deliveryOrder.Dequeue());
        apply(value);
    }
    public void Clear() { _pending.Clear(); _early.Clear(); _earlyOrder.Clear(); _delivered.Clear(); _deliveryOrder.Clear(); }
}
