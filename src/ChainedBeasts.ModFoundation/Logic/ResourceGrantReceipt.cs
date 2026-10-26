using System;

namespace ChainedBeasts.ModFoundation.Logic;

/// <summary>One native execution can acknowledge one actual resource increase, including a capped zero.</summary>
public sealed class ResourceGrantReceipt
{
    public bool Settled { get; private set; }
    public bool TryCommit(int before, int after, out int actual)
    {
        actual = 0;
        if (Settled) return false;
        Settled = true;
        actual = (int)Math.Clamp((long)Math.Max(0, after) - Math.Max(0, before), 0, int.MaxValue);
        return actual > 0;
    }
    public static int MaximumRequest(int maximum, double fraction) => maximum <= 0 || !double.IsFinite(fraction) || fraction <= 0 || fraction > .25
        ? 0 : (int)Math.Clamp(Math.Ceiling(maximum * fraction), 0, int.MaxValue / 4d);
}
