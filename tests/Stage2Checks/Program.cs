using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ChainedBeasts.ModFoundation.Content;
using ChainedBeasts.ModFoundation.State;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.Stats;

var passed = 0;
void Check(bool condition, string name)
{ if (!condition) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
void Reject<T>(Action action, string name) where T : Exception
{ try { action(); } catch (T) { Check(true, name); return; } throw new Exception("FAIL (accepted): " + name); }
ContentDescriptor Definition(string key, int id, ContentCategory category = ContentCategory.Skill, bool enabled = true,
    LegacyMarker legacy = null) => new(key, "test.owner", category, id,
        category == ContentCategory.Skill ? 4 : category == ContentCategory.Affliction ? 3 : 1, enabled, legacy);
var registry = new ContentRegistry((category, id) => id == 10);
var bulk = Definition("pack:skill/bulk", 11000, legacy: new LegacyMarker(ContentCategory.Skill, 10021));
var copy = Definition("copy:skill/separate", 11001);
var injury = Definition("pack:affliction/heavy", 11010, ContentCategory.Affliction, legacy: new LegacyMarker(ContentCategory.Affliction, 4));
registry.Register(bulk); registry.Register(copy); registry.Register(injury);
registry.Register(Definition("copy:equipment/same-number-different-category", 11000, ContentCategory.Equipment));
registry.Register(Definition("disabled:skill/retained", 11002, enabled: false));
Check(registry.Entries.Count == 5, "category IDs are separate namespaces, including disabled claims");
Reject<InvalidOperationException>(() => registry.Register(Definition(copy.Key, 11003)), "duplicate stable key rejected");
Reject<InvalidOperationException>(() => registry.Register(Definition("copy:skill/collision", copy.NumericId)), "duplicate category ID rejected");
Reject<InvalidOperationException>(() => registry.Register(Definition("copy:skill/native", 10)), "installed native enum ID rejected");
Reject<InvalidOperationException>(() => registry.Register(Definition("copy:skill/disabled-collision", 11002)), "disabled content still reserves identity");
Reject<InvalidOperationException>(() => registry.Register(Definition("copy:skill/bad-legacy", 11004, legacy: bulk.Legacy)), "copied legacy marker cannot share storage");
Reject<ArgumentException>(() => registry.Register(Definition("bad-unqualified-key", 11005)), "unqualified key rejected");
Reject<ArgumentException>(() => registry.Register(Definition("copy:equipment/bad-marker", 11005, ContentCategory.Equipment, legacy: bulk.Legacy)), "native ownership cannot claim skill marker");
Reject<InvalidOperationException>(() => registry.RegisterMany(new[] { Definition("batch:first", 11900), Definition("batch:collision", 11000) }),
    "later collision rejects the entire registration batch");
Check(!registry.TryGet("batch:first", out _) && registry.Entries.Count == 5,
    "failed plugin batch leaves no partial identity/offer claims");
registry.RegisterMany(new[] { Definition("batch:valid-first", 11900), Definition("batch:valid-second", 11901) });
Check(registry.TryGet("batch:valid-first", out _) && registry.TryGet("batch:valid-second", out _),
    "validated batch publishes all independent claims together");

// Synthetic native checkpoint JSON exercises production canonicalization and SaveData extraction.
string Native(int token, int seed = 123, int start = 999, int playerId = 0, int hp = 60) => JsonSerializer.Serialize(new
{
    runSeed = seed, runStartTime = start, playerCount = 1, completedLevels = 2,
    playerDatas = new[] { new { playerID = playerId, hp, skillLevels = new[] { token == 0 ? 0 : CheckpointEnvelope.Marker, 0 },
        skillOrder = new[] { token, 0 }, injuryLevels = new[] { 0, 0 }, injuryOrder = new[] { 0, 0 } } }
});
CheckpointIdentity Id(int token, int seed = 123, int start = 999, int playerId = 0, int hp = 60) =>
    CheckpointJson.Identify(Native(token, seed, start, playerId, hp), "Solo", 0);
var id1 = Id(1);
var id2 = Id(2);
Check(id1.RunKey == id2.RunKey && id1.NativeFingerprint != id2.NativeFingerprint, "generation discriminator distinguishes otherwise identical native checkpoints");
Check(CheckpointJson.Fingerprint("{\"b\":2,\"a\":[1,3]}") == CheckpointJson.Fingerprint(" { \"a\": [1,3], \"b\":2 } "), "format and object property order do not change checkpoint hash");
Check(CheckpointJson.Fingerprint("[1,3]") != CheckpointJson.Fingerprint("[3,1]"), "native array order remains significant");
var save = "{\"CheckpointSolo\":{\"runData\":" + Native(1) + "},\"CheckpointOnline\":null}";
Check(CheckpointJson.FromSave(save, 0).Single().NativeFingerprint == id1.NativeFingerprint, "actual serialized SaveData checkpoint selects exact generation");
Reject<InvalidOperationException>(() => CheckpointJson.Identify(Native(1).Replace("\"skillOrder\":[1,0]", "\"skillOrder\":[0,0]"), "Solo", 0), "invalid generation token refused");
Reject<ArgumentException>(() => CheckpointJson.Identify(Native(1), "GuessMode", 0), "unknown native game mode refused");
Reject<InvalidOperationException>(() => CheckpointJson.Identify(Native(1).Replace(CheckpointEnvelope.Marker.ToString(), "-20302"), "Solo", 0),
    "future checkpoint marker protocol cannot silently fall back to legacy");

CheckpointEnvelope Snapshot(CheckpointIdentity id, int rank, int severity)
{
    var state = new PlayerContentState();
    state.Ownership.Add(bulk.Key, new OwnedContentState(ContentCategory.Skill, bulk.NumericId, rank, 2));
    state.Ownership.Add(copy.Key, new OwnedContentState(ContentCategory.Skill, copy.NumericId, 2, 5));
    state.Ownership.Add(injury.Key, new OwnedContentState(ContentCategory.Affliction, injury.NumericId, severity, 7));
    state.Ownership.Add("future:surgery/unknown", new OwnedContentState(ContentCategory.Surgery, 12001, 1, 0));
    state.Ownership.Add("disabled:skill/retained", new OwnedContentState(ContentCategory.Skill, 11002, 1, 3));
    state.SetFeature("copy:per-battle/used", new[] { 2, 4 });
    state.SetFeature("foundation:battle-key", id.RunKey + "/level/2");
    return new CheckpointEnvelope { Identity = id, LegacyImported = true, Players = new() { [id.PlayerIds[0]] = state } };
}
var root = Path.Combine(Path.GetTempPath(), "ChainedBeasts-Stage2Checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var store = new CheckpointStore(root);
    var first = Snapshot(id1, 1, 2);
    store.Prepare(first);
    Check(store.HasPrepared(id1), "complete snapshot staged atomically before native write");
    Reject<FileNotFoundException>(() => store.Read(id1), "pending snapshot cannot restore without native-file proof");
    store.Commit(id1, false);
    Reject<FileNotFoundException>(() => store.Read(id1), "failed native write does not publish custom state");
    Check(store.AllocateToken(id1, new HashSet<int>()) == 2, "pending generations cannot be reused");
    store.Commit(id1, true);
    var restored = new CheckpointStore(root).Read(id1);
    Check(restored.Players[0].Ownership[bulk.Key].Level == 1 && restored.Players[0].Ownership[copy.Key].Level == 2,
        "original and copied skill ownership restore independently across store restart");
    Check(restored.Players[0].Ownership[copy.Key].Order == 5 && restored.Players[0].Ownership[injury.Key].Level == 2,
        "independent skill order and affliction severity survive round trip");
    Check(restored.Players[0].GetFeature<int[]>("copy:per-battle/used").SequenceEqual(new[] { 2, 4 }), "namespaced feature state round trips");
    Check(StateProjection.Active(restored.Players[0], registry).Count == 3, "unknown and disabled entries retained dormant without application");
    Reject<InvalidOperationException>(() => StateProjection.RequireAvailableOwnership(restored.Players[0], registry),
        "actual run restoration blocked if owned content is missing or disabled; storage remains intact");
    var onlyDisabled = new PlayerContentState(); onlyDisabled.Ownership.Add("disabled:skill/retained", restored.Players[0].Ownership["disabled:skill/retained"]);
    Reject<InvalidOperationException>(() => StateProjection.RequireAvailableOwnership(onlyDisabled, registry), "disabled owned entry alone blocks run continuation");
    var projected = restored.Clone();
    Check(projected.Players[0].Ownership.ContainsKey("future:surgery/unknown") && projected.Players[0].Ownership.ContainsKey("disabled:skill/retained"),
        "dormant state survives cloning/resaving without a loaded owner");
    registry.Disable(copy.Key);
    Check(StateProjection.Active(restored.Players[0], registry).Count == 2, "failed/disabled registration stops mechanics without erasing state");
    store.Prepare(first); store.Commit(id1, true);
    Check(store.Read(id1).LegacyImported, "identical re-save and commit are idempotent");
    var rewrite = first.Clone(); rewrite.Players[0].Ownership[bulk.Key] = new OwnedContentState(ContentCategory.Skill, 11000, 3, 2);
    Reject<InvalidDataException>(() => store.Prepare(rewrite), "immutable generation cannot be silently rewritten");
    var second = Snapshot(id2, 3, 1); store.Prepare(second); store.Commit(id2, true);
    Check(store.Read(id1).Players[0].Ownership[bulk.Key].Level == 1 && store.Read(id2).Players[0].Ownership[bulk.Key].Level == 3,
        "restoring older native checkpoint restores older custom state, never latest sidecar");
    Check(store.AllocateToken(id1, new HashSet<int> { 3 }) == 4, "token allocation checks disk and in-memory captures");
    Reject<InvalidDataException>(() => store.Read(Id(1, hp: 59)), "same token with stale native content hash rejected");
    Reject<FileNotFoundException>(() => store.Read(Id(1, seed: 124)), "different run seed cannot borrow snapshot");
    Reject<FileNotFoundException>(() => store.Read(Id(1, start: 1000)), "fresh run with same seed cannot inherit snapshot");
    Reject<InvalidDataException>(() => store.Read(Id(1, playerId: 5)), "player identity mismatch rejected");
    Reject<FileNotFoundException>(() => store.Read(Id(99)), "missing matching sidecar is explicit failure");
    var third = Snapshot(Id(3), 2, 3); store.Prepare(third);
    Check(store.Read(third.Identity, true).Players[0].Ownership[injury.Key].Level == 3,
        "successful native file proves crash-window pending snapshot and recovers it");
    var invalid = first.Clone(); invalid.SchemaVersion = 900;
    Reject<InvalidDataException>(() => store.Prepare(invalid), "unknown schema cannot publish or overwrite existing state");
    var badRoster = first.Clone(); badRoster.Players[99] = badRoster.Players[0];
    Reject<InvalidDataException>(() => store.Prepare(badRoster), "extra or missing saved player rejected");
    var changedIdentity = new ContentRegistry(); changedIdentity.Register(Definition(bulk.Key, 99999));
    Reject<InvalidOperationException>(() => StateProjection.Active(first.Players[0], changedIdentity), "changed content key-to-ID mapping refused");
    var badRank = first.Clone(); badRank.Players[0].Ownership[bulk.Key] = new OwnedContentState(ContentCategory.Skill, 11000, 5, 2);
    Reject<InvalidOperationException>(() => StateProjection.Active(badRank.Players[0], registry), "rank outside installed content definition refused");
    var schemaPath = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Single(path => path.EndsWith("0000000002.json"));
    var newerBytes = JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 900 }); File.WriteAllBytes(schemaPath, newerBytes);
    Reject<InvalidDataException>(() => store.Read(id2), "newer on-disk schema blocked explicitly");
    Check(File.ReadAllBytes(schemaPath).SequenceEqual(newerBytes), "unknown schema file preserved byte for byte");
    Check(!Directory.GetFiles(root, "*.tmp-*", SearchOption.AllDirectories).Any(), "atomic writes leave no partial temporary snapshots");
    var oldFormat = Snapshot(Id(0), 1, 2); store.Prepare(oldFormat); store.Commit(oldFormat.Identity, true);
    Check(new CheckpointStore(root).Read(oldFormat.Identity).LegacyImported,
        "committed legacy-generation migration marker survives process/store restart");
}
finally { Directory.Delete(root, true); } // Only the uniquely generated test directory, never game data.

var migrated = new PlayerContentState();
StateProjection.ImportLegacy(migrated, bulk.Key, bulk, 1, 8);
StateProjection.ImportLegacy(migrated, injury.Key, injury, 3, 4);
StateProjection.ImportLegacy(migrated, bulk.Key, bulk, 1, 999);
Check(migrated.Ownership.Count == 2 && migrated.Ownership[bulk.Key].Order == 8 && migrated.Ownership[injury.Key].Level == 3,
    "legacy bridges import separate ownership once without replaying acquisition");
Reject<ArgumentException>(() => StateProjection.ImportLegacy(migrated, "copy:wrong-key", copy, 1, 1),
    "legacy import cannot assign state to another registered identity");
StateProjection.ImportLegacy(migrated, copy.Key, copy, 0, 1);
Check(!migrated.Ownership.ContainsKey(copy.Key), "empty legacy storage does not grant content");
var session = new RunStateSession(); session.Start(id1.RunKey); session.Players[0] = migrated;
Check(session.EnterBattle(2) && !session.EnterBattle(2), "duplicate scene/state callback does not rearm battle allowance");
session.Restore(Snapshot(id1, 1, 2));
Check(!session.EnterBattle(2) && session.EnterBattle(3), "restoring existing battle preserves consumed scope, next level creates new scope");
Check(!session.Start(id1.RunKey) && session.Players[0].Ownership.Count > 0, "same run does not clear live feature state");
Check(session.Start(Id(1, start: 1000).RunKey) && session.Players.Count == 0 && !session.LegacyImported,
    "fresh run clears ownership, feature state and import marker");
session.Players[0] = migrated; session.Clear();
Check(session.RunKey is null && session.BattleKey is null && session.Players.Count == 0, "abandoned/reset run clears all live state");

var gate = new MutationGate();
var acquire = gate.Begin("player:bulk", 0); var nested = gate.Begin("player:bulk", 0);
Check(!gate.Complete(nested, 1) && gate.Complete(acquire, 1) && !gate.Complete(acquire, 1), "nested and duplicate callbacks emit one real transition");
Check(!gate.Complete(gate.Begin("player:bulk", 1), 1), "no-op acquisition grants nothing");
Check(gate.Complete(gate.Begin("player:bulk", 1), 0) && gate.Complete(gate.Begin("player:bulk", 0), 1), "normal removal then reacquisition are independent real transitions");
Check(!gate.Complete(gate.Begin("player:bulk", 0), 1, restoring: true), "checkpoint assignment never emits acquisition effects");
Check(!gate.Complete(gate.Begin("player:bulk", 0), 1, failed: true) && gate.Complete(gate.Begin("player:bulk", 0), 1), "failed operation releases gate and grants nothing");
Check(OwnershipDelta.Clamp(1, 1, 1, true) == 0 && OwnershipDelta.Clamp(0, 9, 4, true) == 4, "native custom rank increments bounded by definition");
Check(OwnershipDelta.Clamp(1, -1, 4, true) == -1 && OwnershipDelta.Clamp(1, 1, 4, false) == 1,
    "signed AddSkill removal and normal RemoveSkill both observed");
Check(OwnershipDelta.Clamp(1, int.MinValue, 4, false) == -3, "signed rank clamp handles integer overflow safely");
Check(HealthTransition.Scale(60, 100, 200, 200) == 120 && HealthTransition.Scale(120, 200, 100, 100) == 60,
    "Bulk Up acquire/remove preserves health percentage");
Check(HealthTransition.Scale(0, 100, 200, 200) == 0 && HealthTransition.Scale(60, 100, 200, 90) == 90,
    "ownership changes do not revive and respect attainable-health cap");
var modifiers = new ModifierPipeline<bool>();
modifiers.Register("pack:max-health", StatChannel.MaximumHealth, owned => owned ? 2d : 1d);
modifiers.Register("copy:max-health", StatChannel.MaximumHealth, owned => owned ? 1.5d : 1d);
Check(modifiers.Evaluate(StatChannel.MaximumHealth, 100, true) == 300 && modifiers.Evaluate(StatChannel.MaximumHealth, 100, true) == 300,
    "multiple independent modifiers compose from fresh native baseline without repeated multiplication");
Check(modifiers.Evaluate(StatChannel.MaximumHealth, 100, false) == 100 && modifiers.Evaluate(StatChannel.MovementSpeed, 8, true) == 8,
    "unowned and unrelated stats retain native behavior");
modifiers.Remove("pack:max-health");
Check(modifiers.Evaluate(StatChannel.MaximumHealth, 100, true) == 150, "removing one modifier preserves the other");
Reject<InvalidOperationException>(() => modifiers.Register("copy:max-health", StatChannel.MaximumHealth, _ => 9), "duplicate modifier key rejected");
modifiers.Register("bad:factor", StatChannel.MovementSpeed, _ => double.NaN);
Reject<InvalidOperationException>(() => modifiers.Evaluate(StatChannel.MovementSpeed, 8, true), "invalid factors fail explicitly rather than corrupt stats");
Check(SoloSessionPolicy.Allows(true, true, true, 1, 1, 1), "one local host in real Solo mode may use developer tools");
Check(!SoloSessionPolicy.Allows(false, true, true, 1, 1, 1) && !SoloSessionPolicy.Allows(true, true, false, 1, 1, 1), "online host and non-host cannot use solo tools");
Check(!SoloSessionPolicy.Allows(true, true, true, 2, 1, 2) && !SoloSessionPolicy.Allows(true, false, true, 1, 1, 1) &&
    !SoloSessionPolicy.Allows(true, true, true, 1, 0, 1), "extra clients, missing session and remote-only client rejected");

var repository = Path.GetFullPath(args[0]); var configuration = args.Length > 1 ? args[1] : "Release";
var examplePath = Path.Combine(repository, $"examples/StandaloneContentExample/bin/{configuration}/net6.0/StandaloneContentExample.dll");
using var pe = new PEReader(File.OpenRead(examplePath)); var metadata = pe.GetMetadataReader();
var refs = metadata.AssemblyReferences.Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)).ToArray();
Check(refs.Contains("ChainedBeasts.ModFoundation") && !refs.Contains("TermsAndContusions"), "compiled standalone example uses Foundation without pack dependency");
Check(Directory.GetFiles(Path.GetDirectoryName(examplePath), "*.dll").Length == 1, "example output contains its DLL only, no copied game or Foundation DLLs");
// Inspect installed generated metadata without Assembly.Load, invoking native code, or starting Unity.
using var gamePe = new PEReader(File.OpenRead(Path.Combine(args[2], "BepInEx/interop/Assembly-CSharp.dll")));
var gameMetadata = gamePe.GetMetadataReader();
TypeDefinition GameType(string name)
{
    foreach (var handle in gameMetadata.TypeDefinitions)
    {
        var type = gameMetadata.GetTypeDefinition(handle);
        var full = gameMetadata.GetString(type.Name);
        var parent = type.GetDeclaringType();
        if (!parent.IsNil) full = gameMetadata.GetString(gameMetadata.GetTypeDefinition(parent).Name) + "+" + full;
        if (full == name) return type;
    }
    throw new Exception("Missing installed interop type: " + name);
}
foreach (var expected in new[]
{
    ("RunDataManager", "CreateCheckpoint", 0), ("RunDataManager", "RestoreCheckpoint", 1),
    ("RunDataManager", "Reset", 0), ("RunDataManager", "StartNewRunRpc", 5),
    ("NetworkSession", "RestoreCheckpoint", 0), ("NetworkSession", "ClearCheckpoint", 0),
    ("PlayerData+Checkpoint", "Validate", 0), ("PlayerData", "RestoreCheckpoint", 1),
    ("PlayerData", "OnCheckpointSynced", 2), ("PlayerData", "AddSkill", 2), ("PlayerData", "RemoveSkill", 2),
    ("PlayerData", "AddInjury", 2), ("PlayerData", "RemoveInjury", 2),
    ("Steam", "WriteFile", 3), ("Standalone", "WriteFile", 3), ("GameLevel", "OnStateChanged", 1),
    ("Player", "ReceiveEquipmentRPC", 1), ("PlayerData", "get_MaxHealth", 0),
    ("Player", "get_MaxAttainableHealth", 0), ("Player", "get_FinalMovementSpeed", 0),
    ("Player", "get_StaminaCostDash", 0), ("Player", "get_StaminaRecoveryRate", 0),
    ("GameLibrary", "Init", 0), ("GameLibrary", "GetAllSkillTypes", 0),
    ("GameLibrary", "GetAllInjuryTypes", 0), ("GameLibrary", "GetAllPurchaseTypes", 0),
    ("StringManager", "GetLocalisedTerm", 1), ("LocalizationManager", "GetTranslation", 8),
    ("LocalizationManager", "TryGetTranslation", 9), ("Localize", "SetTerm", 1)
})
{
    var methods = GameType(expected.Item1).GetMethods().Select(handle => gameMetadata.GetMethodDefinition(handle));
    Check(methods.Count(method => gameMetadata.GetString(method.Name) == expected.Item2 &&
        method.GetParameters().Count(handle => gameMetadata.GetParameter(handle).SequenceNumber > 0) == expected.Item3) == 1,
        $"installed interop contains unique target {expected.Item1}.{expected.Item2}/{expected.Item3}");
}
foreach (var claim in new[] { ("SkillType", 11000), ("PurchaseType", 2499), ("InjuryType", 11010), ("SurgeryType", 11020), ("SkillType", 12000) })
{
    var numeric = GameType(claim.Item1).GetFields().Select(handle => gameMetadata.GetFieldDefinition(handle).GetDefaultValue())
        .Where(handle => !handle.IsNil).Select(handle => gameMetadata.GetBlobReader(gameMetadata.GetConstant(handle).Value).ReadInt32());
    Check(!numeric.Contains(claim.Item2), $"registered {claim.Item1} ID {claim.Item2} is absent from installed native enum");
}
Console.WriteLine($"Stage 2: {passed} meaningful nongame checks passed; no game initialized or deployed.");
