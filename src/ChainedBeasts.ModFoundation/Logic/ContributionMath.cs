using System;

namespace ChainedBeasts.ModFoundation.Logic;

public static class ContributionMath
{
    public static double ScalePositiveGain(double before, double after, double factor) =>
        after > before ? before + (after - before) * Math.Max(0, factor) : after;
}
