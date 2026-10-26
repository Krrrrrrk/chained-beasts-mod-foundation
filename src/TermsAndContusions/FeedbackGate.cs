using System;
using System.Collections.Generic;

namespace TermsAndContusions;

/// <summary>Bounded cosmetic throttle. Rejected/zero benefits never consume a player's cue budget.</summary>
public sealed class FeedbackGate
{
    private readonly Dictionary<int, double> _last = new();
    private readonly double _interval;
    private readonly int _capacity;
    public FeedbackGate(double interval = .75, int capacity = 16)
    {
        if (!double.IsFinite(interval) || interval < 0 || capacity < 1 || capacity > 128) throw new ArgumentOutOfRangeException();
        _interval = interval; _capacity = capacity;
    }
    public bool TryActivate(bool enabled, bool locallyOwned, int playerId, string contentKey, double actualBenefit, double now)
    {
        if (!enabled || !locallyOwned || playerId < 0 || string.IsNullOrWhiteSpace(contentKey) ||
            !contentKey.StartsWith("terms-and-contusions:", StringComparison.Ordinal) ||
            !double.IsFinite(actualBenefit) || actualBenefit <= 0 || !double.IsFinite(now) || now < 0) return false;
        if (_last.TryGetValue(playerId, out var previous))
        {
            if (now < previous || now - previous < _interval) return false;
        }
        else if (_last.Count >= _capacity) return false;
        _last[playerId] = now;
        return true;
    }
    public void Clear() => _last.Clear();
}
