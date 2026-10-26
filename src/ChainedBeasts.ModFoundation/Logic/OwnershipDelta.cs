using System;

namespace ChainedBeasts.ModFoundation.Logic;

public static class OwnershipDelta
{
    // Both native methods accept a signed count. Bound the resulting rank,
    // including removal through AddSkill(-1), without substituting acquisition.
    public static int Clamp(int owned, int amount, int maximum, bool adding)
    {
        var target = Math.Clamp((long)owned + (adding ? (long)amount : -(long)amount), 0L, maximum);
        return checked((int)(adding ? target - owned : owned - target));
    }
}
