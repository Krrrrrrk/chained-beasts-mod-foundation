using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ChainedBeasts.ModFoundation.State;

/// <summary>Immutable generation snapshots. Never restores "the newest" state.</summary>
public sealed class CheckpointStore
{
    private readonly string _root;
    private readonly object _gate = new();
    public CheckpointStore(string root) => _root = Path.GetFullPath(root);

    private string PathFor(CheckpointIdentity identity, bool pending)
    {
        var name = identity.Token > 0 ? identity.Token.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) :
            "legacy-" + identity.NativeFingerprint;
        if (identity.NativeFingerprint is null || identity.NativeFingerprint.Length != 64 || identity.NativeFingerprint.Any(value => !Uri.IsHexDigit(value)))
            throw new InvalidDataException("Invalid checkpoint fingerprint.");
        return Path.Combine(_root, identity.RunKey, name + (pending ? ".pending" : ".json"));
    }

    public int AllocateToken(CheckpointIdentity run, System.Collections.Generic.ISet<int> pendingTokens)
    {
        lock (_gate)
        {
            for (var token = 1; token < int.MaxValue; token++)
            {
                var candidate = run.WithToken(token);
                if (!pendingTokens.Contains(token) && !File.Exists(PathFor(candidate, false)) && !File.Exists(PathFor(candidate, true)))
                    return token;
            }
            throw new InvalidOperationException("Checkpoint token space exhausted.");
        }
    }

    public void Prepare(CheckpointEnvelope envelope)
    {
        Validate(envelope, envelope.Identity);
        lock (_gate)
        {
            foreach (var pending in new[] { false, true })
            {
                var existing = PathFor(envelope.Identity, pending);
                if (!File.Exists(existing)) continue;
                var saved = ReadFile(existing, envelope.Identity);
                if (CheckpointJson.Fingerprint(JsonSerializer.Serialize(saved)) != CheckpointJson.Fingerprint(JsonSerializer.Serialize(envelope)))
                    throw new InvalidDataException("Attempt to rewrite an immutable checkpoint generation.");
                return;
            }
            AtomicCreate(PathFor(envelope.Identity, true), JsonSerializer.SerializeToUtf8Bytes(envelope));
        }
    }

    public void Commit(CheckpointIdentity identity, bool nativeWriteSucceeded)
    {
        if (!nativeWriteSucceeded) return;
        lock (_gate)
        {
            if (File.Exists(PathFor(identity, false))) { ReadFile(PathFor(identity, false), identity); return; }
            ReadFile(PathFor(identity, true), identity);
            File.Move(PathFor(identity, true), PathFor(identity, false));
        }
    }

    public CheckpointEnvelope Read(CheckpointIdentity identity, bool matchingNativeFileProvesPendingWrite = false)
    {
        lock (_gate)
        {
            var committed = PathFor(identity, false);
            if (File.Exists(committed)) return ReadFile(committed, identity);
            if (matchingNativeFileProvesPendingWrite && File.Exists(PathFor(identity, true)))
            {
                Commit(identity, true); // Recovery requires the actual persisted native checkpoint, not live state.
                return ReadFile(committed, identity);
            }
            throw new FileNotFoundException("Matching custom checkpoint snapshot is missing.", committed);
        }
    }

    public bool HasPrepared(CheckpointIdentity identity)
    {
        lock (_gate)
        {
            var path = File.Exists(PathFor(identity, false)) ? PathFor(identity, false) : PathFor(identity, true);
            if (!File.Exists(path)) return false;
            ReadFile(path, identity);
            return true;
        }
    }

    private static CheckpointEnvelope ReadFile(string path, CheckpointIdentity identity)
    {
        var envelope = JsonSerializer.Deserialize<CheckpointEnvelope>(File.ReadAllBytes(path));
        Validate(envelope, identity);
        return envelope;
    }

    public static void Validate(CheckpointEnvelope envelope, CheckpointIdentity expected)
    {
        if (envelope is null || envelope.SchemaVersion != CheckpointEnvelope.CurrentSchema)
            throw new InvalidDataException("Unsupported custom checkpoint schema; file preserved without rewriting.");
        var actual = envelope.Identity;
        if (expected?.PlayerIds is null || actual?.PlayerIds is null ||
            actual.RunKey != expected.RunKey || actual.Token != expected.Token ||
            actual.NativeFingerprint != expected.NativeFingerprint || !actual.PlayerIds.SequenceEqual(expected.PlayerIds))
            throw new InvalidDataException("Native checkpoint/custom snapshot association mismatch.");
        if (actual.Mode is not ("Solo" or "Local" or "Online") || actual.Token < 0 ||
            actual.PlayerIds.Length == 0 || actual.PlayerIds.Distinct().Count() != actual.PlayerIds.Length)
            throw new InvalidDataException("Invalid custom checkpoint identity.");
        if (envelope.Players is null || !envelope.Players.Keys.OrderBy(value => value).SequenceEqual(expected.PlayerIds.OrderBy(value => value)))
            throw new InvalidDataException("Custom snapshot player roster mismatch.");
        foreach (var player in envelope.Players.Values)
        {
            if (player?.Ownership is null || player.Features is null) throw new InvalidDataException("Invalid custom player state.");
            foreach (var entry in player.Ownership)
                if (entry.Value is null || entry.Value.Level < 0 || entry.Value.Order < 0 || entry.Value.NumericId < 0)
                    throw new InvalidDataException("Invalid custom ownership state: " + entry.Key);
        }
    }

    private static void AtomicCreate(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, path); // Same-directory atomic publication; no partial JSON at the target name.
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
