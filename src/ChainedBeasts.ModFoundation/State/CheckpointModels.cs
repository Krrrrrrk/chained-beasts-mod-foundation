using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChainedBeasts.ModFoundation.Content;

namespace ChainedBeasts.ModFoundation.State;

public sealed class CheckpointIdentity
{
    public string Mode { get; }
    public int RunSeed { get; }
    public int RunStartTime { get; }
    public int Token { get; }
    public string NativeFingerprint { get; }
    public int[] PlayerIds { get; }
    public CheckpointIdentity(string mode, int runSeed, int runStartTime, int token, string nativeFingerprint, int[] playerIds)
    { Mode = mode; RunSeed = runSeed; RunStartTime = runStartTime; Token = token; NativeFingerprint = nativeFingerprint; PlayerIds = playerIds; }
    public CheckpointIdentity WithToken(int token) => new(Mode, RunSeed, RunStartTime, token, NativeFingerprint, PlayerIds);
    public string RunKey => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        Mode + ":" + RunSeed.ToString(CultureInfo.InvariantCulture) + ":" + RunStartTime.ToString(CultureInfo.InvariantCulture)))).ToLowerInvariant();
}

public sealed class OwnedContentState
{
    public ContentCategory Category { get; }
    public int NumericId { get; }
    public int Level { get; }
    public int Order { get; }
    public OwnedContentState(ContentCategory category, int numericId, int level, int order)
    { Category = category; NumericId = numericId; Level = level; Order = order; }
}

public sealed class PlayerContentState
{
    public Dictionary<string, OwnedContentState> Ownership { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonElement> Features { get; set; } = new(StringComparer.Ordinal);

    public void SetFeature<T>(string namespacedKey, T value)
    {
        if (string.IsNullOrWhiteSpace(namespacedKey) || !namespacedKey.Contains(':'))
            throw new ArgumentException("Feature state needs a stable namespaced key.");
        Features[namespacedKey] = JsonSerializer.SerializeToElement(value);
    }

    public T GetFeature<T>(string key, T fallback = default) =>
        Features.TryGetValue(key, out var value) ? value.Deserialize<T>() : fallback;
}

public sealed class CheckpointEnvelope
{
    public const int CurrentSchema = 1;
    // One shared checkpoint discriminator in the already reserved None slot.
    // This never stores a content rank; every content value lives in the sidecar.
    public const int Marker = -20301;
    public int SchemaVersion { get; set; } = CurrentSchema;
    public CheckpointIdentity Identity { get; set; }
    public bool LegacyImported { get; set; }
    public Dictionary<int, PlayerContentState> Players { get; set; } = new();

    public CheckpointEnvelope Clone() => JsonSerializer.Deserialize<CheckpointEnvelope>(JsonSerializer.Serialize(this));
}

public static class CheckpointJson
{
    public static string Fingerprint(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, document.RootElement);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(value => value.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var value in element.EnumerateArray()) WriteCanonical(writer, value);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }

    public static CheckpointIdentity Identify(string runCheckpointJson, string mode, int noneIndex)
    {
        if (mode is not ("Solo" or "Local" or "Online")) throw new ArgumentException("Unknown native game mode.");
        using var document = JsonDocument.Parse(runCheckpointJson);
        var root = document.RootElement;
        var players = root.GetProperty("playerDatas").EnumerateArray().ToArray();
        var count = root.GetProperty("playerCount").GetInt32();
        if (count < 1 || count > players.Length) throw new InvalidOperationException("Invalid checkpoint player count.");
        var ids = players.Take(count).Select(value => value.GetProperty("playerID").GetInt32()).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new InvalidOperationException("Duplicate checkpoint player identity.");
        var levels = players[0].GetProperty("skillLevels");
        var orders = players[0].GetProperty("skillOrder");
        var marker = levels[noneIndex].GetInt32();
        if (marker < 0 && marker != CheckpointEnvelope.Marker)
            throw new InvalidOperationException("Unknown checkpoint discriminator protocol; native data preserved.");
        var token = marker == CheckpointEnvelope.Marker ? orders[noneIndex].GetInt32() : 0;
        if (marker == CheckpointEnvelope.Marker && token <= 0)
            throw new InvalidOperationException("Invalid custom checkpoint discriminator.");
        return new CheckpointIdentity(mode, root.GetProperty("runSeed").GetInt32(),
            root.GetProperty("runStartTime").GetInt32(), token, Fingerprint(runCheckpointJson), ids);
    }

    public static IEnumerable<CheckpointIdentity> FromSave(string saveJson, int noneIndex)
    {
        using var document = JsonDocument.Parse(saveJson);
        foreach (var mode in new[] { "Solo", "Local", "Online" })
            if (document.RootElement.TryGetProperty("Checkpoint" + mode, out var checkpoint) && checkpoint.ValueKind != JsonValueKind.Null &&
                checkpoint.TryGetProperty("runData", out var runData) && runData.ValueKind != JsonValueKind.Null)
                yield return Identify(runData.GetRawText(), mode, noneIndex);
    }
}

public static class StateProjection
{
    public static void RequireAvailableOwnership(PlayerContentState state, ContentRegistry registry)
    {
        Active(state, registry); // Also validate known identity mappings and supported ranks.
        foreach (var entry in state.Ownership)
            if (entry.Value.Level > 0 && (!registry.TryGet(entry.Key, out var definition) || !definition.Enabled))
                throw new InvalidOperationException("Saved ownership requires its compatible enabled content plugin: " + entry.Key);
    }
    public static IReadOnlyList<KeyValuePair<string, OwnedContentState>> Active(PlayerContentState state, ContentRegistry registry)
    {
        var result = new List<KeyValuePair<string, OwnedContentState>>();
        foreach (var entry in state.Ownership)
        {
            if (!registry.TryGet(entry.Key, out var definition)) continue; // Retained dormant, never guessed.
            if (definition.NumericId != entry.Value.NumericId || definition.Category != entry.Value.Category)
                throw new InvalidOperationException("Saved identity mapping changed: " + entry.Key);
            if (!definition.Enabled) continue;
            if (entry.Value.Level < 0 || entry.Value.Level > definition.MaximumLevel)
                throw new InvalidOperationException("Saved rank/severity exceeds supported definition: " + entry.Key);
            result.Add(entry);
        }
        return result;
    }

    public static void ImportLegacy(PlayerContentState state, string key, ContentDescriptor definition, int level, int order)
    {
        if (key != definition.Key) throw new ArgumentException("Legacy key does not match registered identity.");
        if (state.Ownership.ContainsKey(key) || level <= 0) return;
        state.Ownership.Add(key, new OwnedContentState(definition.Category, definition.NumericId,
            Math.Min(level, definition.MaximumLevel), order));
    }
}
