using System.Collections.Generic;

namespace ChainedBeasts.ModFoundation.Logic;

/// <summary>Ordered local RPC dispatches, including a null direct-contact token for generated/unrelated damage.</summary>
public sealed class DamageReceiptQueue<T> where T : class
{
    private readonly Dictionary<string, Queue<T>> _pending = new();
    public int Count { get; private set; }
    public void Enqueue(string nativeRoute, T contact)
    { if (!_pending.TryGetValue(nativeRoute, out var queue)) _pending[nativeRoute] = queue = new Queue<T>(); queue.Enqueue(contact); Count++; }
    public bool TryDequeue(string nativeRoute, out T contact)
    {
        contact = null;
        if (!_pending.TryGetValue(nativeRoute, out var queue)) return false;
        contact = queue.Dequeue(); Count--; if (queue.Count == 0) _pending.Remove(nativeRoute); return true;
    }
    public void Clear() { _pending.Clear(); Count = 0; }
}
