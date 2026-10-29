using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.Networking;

namespace ChainedBeasts.ModFoundation.Runtime;

public static class ContentOfferServices
{
    private static readonly ContentOfferSelection Selection = new();
    public static bool SessionLocked => Selection.SessionLocked;
    public static bool Allows(string key) => Selection.Allows(key);
    public static void Register(string key, bool enabled = true)
    {
        if (!FoundationServices.Content.TryGet(key, out _)) throw new InvalidOperationException("Register the content identity first.");
        Selection.Register(key, enabled);
    }
    public static Dictionary<string, bool> LocalPreferences() => Selection.Local();
    public static Dictionary<string, bool> EffectivePreferences() => Selection.Effective();
    public static void SaveLocal(IReadOnlyDictionary<string, bool> values, Action persist)
    {
        if (Unity.Netcode.NetworkManager.Singleton?.IsListening == true)
            throw new InvalidOperationException("Leave the party/run before changing TC content.");
        Selection.SaveLocal(values, persist);
    }
    internal static string Snapshot()
    {
        var json = JsonSerializer.Serialize(Selection.Effective().OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value));
        if (json.Length > 32768) throw new InvalidOperationException("Host offer selection exceeds its bounded size.");
        return json;
    }
    internal static string Digest(string json) => MultiplayerCodec.Fingerprint(new[] { json });
    internal static void Adopt(string json, string digest)
    {
        if (json is null || json.Length > 32768 || Digest(json) != digest) throw new InvalidOperationException("Invalid host offer selection.");
        Selection.AdoptHost(JsonSerializer.Deserialize<Dictionary<string, bool>>(json));
    }
    internal static void FreezeHost() => Selection.AdoptHost(Selection.Local());
    internal static void ResetSession() => Selection.EndSession();
}
