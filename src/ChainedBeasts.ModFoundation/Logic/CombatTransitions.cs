using System;
using System.Collections.Generic;

namespace ChainedBeasts.ModFoundation.Logic;

public sealed class AttackAttemptLedger<T>
{
    private readonly Dictionary<int, (string Key, T Value)> _last = new();
    public T GetOrCreate(int playerId, string nativeAttemptKey, Func<T> create, out bool created)
    {
        if (_last.TryGetValue(playerId, out var prior) && prior.Key == nativeAttemptKey) { created = false; return prior.Value; }
        var value = create(); _last[playerId] = (nativeAttemptKey, value); created = true; return value;
    }
    public void Clear() => _last.Clear();
}

public static class DurabilityTransition
{
    public static bool IsBreak(int beforeEffective, int afterRaw, int positiveLoss, bool nativeDestroyedDuringLoss, bool isDefault) =>
        !isDefault && positiveLoss > 0 && beforeEffective > 0 && (afterRaw <= 0 || nativeDestroyedDuringLoss);
}
