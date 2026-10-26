using System;
using System.Collections.Generic;
using System.Linq;

namespace ChainedBeasts.ModFoundation.Logic;

public sealed class HostGoldSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string[] Backstabbed { get; set; } = Array.Empty<string>();
    public double LooseRemaining { get; set; }
}
/// <summary>Host's independent once-per-enemy cap and three-second loss cooldown, checkpointed without replay.</summary>
public sealed class HostGoldGuard
{
    private readonly HashSet<string> _targets = new(StringComparer.Ordinal);
    private double _looseUntil;
    public bool ClaimBackstab(string target)
    { return !string.IsNullOrWhiteSpace(target) && _targets.Count < 10 && _targets.Add(target); }
    public bool ClaimLoss(double now)
    {
        if (!double.IsFinite(now) || now < 0 || now < _looseUntil) return false;
        _looseUntil = now + 3; return true;
    }
    public HostGoldSnapshot Capture(double now) => new() { Backstabbed = _targets.OrderBy(x => x, StringComparer.Ordinal).ToArray(), LooseRemaining = Math.Clamp(_looseUntil - now, 0, 3) };
    public static HostGoldGuard Restore(HostGoldSnapshot state, double now)
    {
        if (state is null || state.SchemaVersion != 1 || state.Backstabbed is null || state.Backstabbed.Length > 10 ||
            state.Backstabbed.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 256) || state.Backstabbed.Distinct(StringComparer.Ordinal).Count() != state.Backstabbed.Length ||
            !double.IsFinite(state.LooseRemaining) || state.LooseRemaining < 0 || state.LooseRemaining > 3 || !double.IsFinite(now) || now < 0)
            throw new InvalidOperationException("Invalid host gold guard checkpoint.");
        var guard = new HostGoldGuard { _looseUntil = now + state.LooseRemaining };
        foreach (var target in state.Backstabbed) guard._targets.Add(target);
        return guard;
    }
}
