using System;
using System.Collections.Generic;
using System.Linq;

namespace ChainedBeasts.ModFoundation.Logic;

public static class SurgeryEligibility
{
    public static bool CanAcquire(bool enabled, bool providerPresent, bool owned, int configuredPrice, int offeredPrice, int money, bool nested) =>
        enabled && providerPresent && !owned && !nested && configuredPrice >= 0 && offeredPrice == configuredPrice && money >= configuredPrice;
}

/// <summary>A native completion receipt, never a money-only inference.</summary>
public sealed class SurgeryCompletion
{
    private bool _completed, _committed;
    public bool Observe(bool samePlayer, int award, int value)
    { if (samePlayer && award == 35 && value == 1) _completed = true; return _completed; }
    public bool Commit(bool normalReturn, bool owned, bool paid)
    {
        if (_committed || !_completed || !normalReturn || owned || !paid) return false;
        _committed = true; return true;
    }
}

public static class SurgeryOffers
{
    /// <summary>Sample one combined eligible pool without advancing Unity's RNG. Protected native slots stay outside it.</summary>
    public static int[] Select(int nativeCount, int customCount, int slots, int seed, int forcedCustom = -1)
    {
        if (nativeCount < 0 || customCount < 0 || slots < 0) throw new ArgumentOutOfRangeException();
        var candidates = Enumerable.Range(0, nativeCount + customCount).ToList();
        var result = new List<int>();
        if (forcedCustom >= 0 && forcedCustom < customCount && slots > 0)
        { result.Add(nativeCount + forcedCustom); candidates.Remove(nativeCount + forcedCustom); }
        uint state = unchecked((uint)seed) ^ 0x9e3779b9u;
        while (result.Count < slots && candidates.Count > 0)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            var at = (int)(state % (uint)candidates.Count);
            result.Add(candidates[at]); candidates.RemoveAt(at);
        }
        return result.ToArray();
    }
}
