using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChainedBeasts.ModFoundation.State;

/// <summary>The inspected NGO checkpoint representation, separate from the exact persisted JSON identity.</summary>
public static class CheckpointWireProjection
{
    public static string Fingerprint(string json, int awardCount)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Invalid native checkpoint object.");
        var players = Trim(root, "playerCount", "playerDatas");
        if (players.Count is < 1 or > 4) throw new InvalidDataException("Invalid native checkpoint roster.");
        foreach (var node in players) NormalizePlayer(node as JsonObject ?? throw new InvalidDataException("Missing native checkpoint player."), awardCount);
        Trim(root, "purchaseCount", "purchases");
        Trim(root, "previousMissionCount", "previousMissions");
        // Native NetworkSerialize changes a null arena string to the empty string on the writer.
        if (root["previousArena"] is null) root["previousArena"] = "";
        return CheckpointJson.Fingerprint(root.ToJsonString());
    }

    public static string PlayerFingerprint(string json, int awardCount)
    {
        var player = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Invalid native checkpoint player.");
        NormalizePlayer(player, awardCount);
        return CheckpointJson.Fingerprint(player.ToJsonString());
    }

    private static void NormalizePlayer(JsonObject player, int awardCount)
    {
        // This is the only PlayerData.Checkpoint field omitted by the game's NetworkSerialize.
        player.Remove("playerName");
        Trim(player, "skillCount", "skillLevels"); Trim(player, "skillCount", "skillOrder");
        Trim(player, "injuryCount", "injuryLevels"); Trim(player, "injuryCount", "injuryOrder");
        Trim(player, "previousSurgeryCount", "previousSurgeries");
        if (awardCount < 1) throw new InvalidDataException("Invalid native award enumeration.");
        TrimTo(player, "awardStats", awardCount);
    }

    private static JsonArray Trim(JsonObject parent, string countName, string arrayName) =>
        TrimTo(parent, arrayName, parent[countName]?.GetValue<int>() ?? throw new InvalidDataException("Missing checkpoint count: " + countName));

    private static JsonArray TrimTo(JsonObject parent, string arrayName, int count)
    {
        if (count < 0) throw new InvalidDataException("Negative checkpoint array count.");
        if (parent[arrayName] is null && count == 0) parent[arrayName] = new JsonArray();
        var values = parent[arrayName] as JsonArray ?? throw new InvalidDataException("Missing checkpoint array: " + arrayName);
        if (count > values.Count) throw new InvalidDataException("Checkpoint array is shorter than its native count: " + arrayName);
        while (values.Count > count) values.RemoveAt(values.Count - 1);
        return values;
    }
}

/// <summary>One explicitly authorized native transfer, scoped to a run, epoch and checkpoint generation.</summary>
public sealed class CheckpointWireBinding
{
    private CheckpointEnvelope _envelope;
    private string _wireFingerprint, _epoch, _generation, _snapshotFingerprint;
    public string Generation => _generation;
    public bool HasAuthorization => _envelope is not null;

    public void Authorize(CheckpointEnvelope envelope, string wireFingerprint, string epoch, string generation)
    {
        CheckpointStore.Validate(envelope, envelope?.Identity);
        if (string.IsNullOrWhiteSpace(epoch) || string.IsNullOrWhiteSpace(generation) ||
            wireFingerprint is null || wireFingerprint.Length != 64 || wireFingerprint.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Invalid checkpoint transfer authorization.");
        var snapshotFingerprint = CheckpointJson.Fingerprint(JsonSerializer.Serialize(envelope));
        if (_generation == generation && (_wireFingerprint != wireFingerprint || _epoch != epoch || _snapshotFingerprint != snapshotFingerprint))
            throw new InvalidDataException("Checkpoint transfer generation was reused for different state.");
        _envelope = envelope.Clone(); _wireFingerprint = wireFingerprint; _epoch = epoch; _generation = generation; _snapshotFingerprint = snapshotFingerprint;
    }

    public bool TryResolve(string nativeJson, string mode, int noneIndex, int awardCount, string epoch, out CheckpointEnvelope envelope)
    {
        envelope = null;
        if (_envelope is null || epoch != _epoch) return false;
        var candidate = CheckpointJson.Identify(nativeJson, mode, noneIndex);
        var expected = _envelope.Identity;
        if (candidate.RunKey != expected.RunKey || candidate.Token != expected.Token ||
            !candidate.PlayerIds.SequenceEqual(expected.PlayerIds) || CheckpointWireProjection.Fingerprint(nativeJson, awardCount) != _wireFingerprint)
            return false;
        envelope = _envelope.Clone(); return true;
    }

    public void RetainRun(string runKey) { if (_envelope is not null && _envelope.Identity.RunKey != runKey) Clear(); }
    public void Clear() { _envelope = null; _wireFingerprint = _epoch = _generation = _snapshotFingerprint = null; }
}
