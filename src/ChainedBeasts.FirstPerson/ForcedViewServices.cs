#if TC_EMBEDDED_CAMERA
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#endif
#if TC_EMBEDDED_CAMERA
namespace TermsAndContusions.FirstPerson;
#else
namespace ChainedBeasts.FirstPerson;
#endif

/// <summary>One camera owner shared by the F9 toggle and temporary content effects.</summary>
public static class ForcedViewServices
{
    private static readonly Dictionary<string, Func<Player, bool>> Sources = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Failed = new(StringComparer.Ordinal);
    public static bool Available => FirstPersonController.Instance?.Ready == true;

    // Managed API only. Register once during plugin load; the callback must only
    // inspect this local player's live state, without granting/changing content.
    public static void Register(string key, Func<Player, bool> active)
    {
        if (string.IsNullOrWhiteSpace(key) || active is null) throw new ArgumentException("A force source needs an identity and predicate.");
        if (!Sources.TryAdd(key, active)) throw new InvalidOperationException("Duplicate forced-view source: " + key);
    }
    public static void Unregister(string key) { Sources.Remove(key); Failed.Remove(key); }
    internal static bool For(Player player)
    {
        if (player == null) return false;
        var forced = false;
        foreach (var source in Sources)
        {
            if (Failed.Contains(source.Key)) continue;
            try { forced |= source.Value(player); }
            catch (Exception error)
            {
                Failed.Add(source.Key);
                FirstPersonController.Instance?.ReportForceFailure(source.Key, error);
            }
        }
        return forced;
    }
}
