using System;

namespace ChainedBeasts.ModFoundation.Logic;

public static class DamageComposition
{
    public static double MultiplyExceptFlat(double damage, double flat, double multiplier) =>
        Math.Max(0, damage - Math.Max(0, flat)) * Math.Max(0, multiplier) + Math.Max(0, flat);
}
