using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.Stats;
using TermsAndContusions.Skills;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
void Near(double value, double expected, string name, double tolerance = 1e-8) => Check(Math.Abs(value - expected) <= tolerance, name);
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { Check(true, name); return; } throw new Exception("FAIL (accepted): " + name); }
Check(SkillDefinitions.All.Length == 12 && SkillDefinitions.All.Select(value => value.Id).Distinct().Count() == 12 &&
    SkillDefinitions.All.Select(value => value.Key).Distinct().Count() == 12, "twelve distinct explicit skill identities");
var expectedThrow = new[] { new[] { 1d, 2d, 3d, 4d, 5d }, new[] { 2d, 4d, 6d, 8d, 10d } };
for (var rank = 0; rank <= 4; rank++)
{
    Near(SkillMath.BulkUp(rank), rank + 1, $"Bulk Up cumulative max at rank {rank}");
    Near(HealthTransition.Scale(60, 100, 100 * (rank + 1), 100 * (rank + 1)), 60 * (rank + 1), $"Bulk Up gain preserves 60% HP at rank {rank}");
    Near(HealthTransition.Scale(60 * (rank + 1), 100 * (rank + 1), 100, 100), 60, $"Bulk Up removal restores 60% HP from rank {rank}");
    Near(SkillMath.Measured(rank, .8), 1 + rank * .25, $"Measured Violence inclusive 80% rank {rank}");
    Near(SkillMath.Measured(rank, .799999), 1, $"Measured Violence below threshold rank {rank}");
    Near(SkillMath.Fumes(rank, .249999), 1 + rank * .35, $"Fumes below 25% rank {rank}");
    Near(SkillMath.Fumes(rank, .25), 1, $"Fumes excludes exactly 25% rank {rank}");
    Near(SkillMath.GoldDamage(rank, 9), 1, $"gold floors below ten rank {rank}");
    Near(SkillMath.GoldDamage(rank, 19), 1 + .01 * rank, $"gold floors complete tens rank {rank}");
    Near(SkillMath.GoldDamage(rank, int.MaxValue), 1 + .25 * rank, $"gold cap rank {rank}");
    Near(SkillMath.GoldDamage(rank, -10), 1, $"negative gold neutral rank {rank}");
    Near(SkillMath.Fresh(rank, 100, 100), 1 + .3 * rank, $"Fresh Meat uses pre-hit full effective HP rank {rank}");
    Near(SkillMath.Fresh(rank, 99, 100), 1, $"Fresh Meat excludes damaged targets rank {rank}");
    Near(SkillMath.Pick(rank, true), 1 + .5 * rank, $"Pick Your Moment rank {rank}");
    Near(SkillMath.Mixed(rank), 1 + .3 * rank, $"Mixed Martial Arts rank {rank}");
    Near(SkillMath.PaybackGain(rank, 10), 5 * rank, $"Payback fills at required rate rank {rank}");
    Near(SkillMath.PaybackCap(100), 25, $"Payback cap remains 25% at rank {rank}");
    Near(SkillMath.BreakStamina(rank, 100), 25 * rank, $"Waste Not stamina rank {rank}");
    Near(SkillMath.Incoming(rank, 99), 1 - .2 * rank, $"Breathing Room cap rank {rank}");
    Near(SkillMath.Incoming(rank, 1), 1 - .05 * rank, $"Breathing Room per enemy rank {rank}");
    Near(SkillMath.StationaryDelayRate(rank, false), 1, $"moving does not accelerate recovery delay rank {rank}");
    Near(1d / SkillMath.StationaryDelayRate(rank, true), 1d / (rank + 1), $"stationary recovery delay inverse rank {rank}");
    foreach (var sling in new[] { false, true })
    {
        var pipeline = new ModifierPipeline<bool>();
        pipeline.Register("skills:longshot", StatChannel.ThrowRange, _ => SkillMath.ThrowMultiplier(rank, false));
        pipeline.Register("equipment:sling", StatChannel.ThrowRange, equipped => SkillMath.ThrowMultiplier(0, equipped));
        var multiplier = pipeline.Evaluate(StatChannel.ThrowRange, 1, sling);
        Near(multiplier, expectedThrow[sling ? 1 : 0][rank], $"independent Longshot/sling stacking r{rank} sling={sling}");
        Near(ThrowPhysics.ChargeTime(2, multiplier), 2 / expectedThrow[sling ? 1 : 0][rank], $"inverse charging interval r{rank} sling={sling}");
        var advanced = 0d; var elapsed = 0d; const double dt = .001;
        while (advanced < 2 - 1e-9) { advanced += dt + ThrowPhysics.ExtraClockStep(dt, multiplier); elapsed += dt; }
        Check(Math.Abs(elapsed - 2 / multiplier) <= dt + 1e-8, $"runtime extra-clock integration matches inverse time r{rank} sling={sling}");
        foreach (var angle in new[] { 0d, 15d, 45d, 80d })
        foreach (var height in new[] { 0d, 1.5d, 5d })
        {
            const double force = 12d, gravity = 9.81d;
            var changed = ThrowPhysics.Extend(force, angle, multiplier);
            var originalHorizontal = force * Math.Cos(angle * Math.PI / 180);
            var originalVertical = force * Math.Sin(angle * Math.PI / 180);
            var horizontal = changed.Force * Math.Cos(changed.AngleDegrees * Math.PI / 180);
            var vertical = changed.Force * Math.Sin(changed.AngleDegrees * Math.PI / 180);
            var oldFlight = (originalVertical + Math.Sqrt(originalVertical * originalVertical + 2 * gravity * height)) / gravity;
            var flight = (vertical + Math.Sqrt(vertical * vertical + 2 * gravity * height)) / gravity;
            Near(vertical, originalVertical, $"vertical velocity preserved r{rank}/{sling}/{angle}/{height}");
            Near(horizontal * flight, originalHorizontal * oldFlight * multiplier, $"actual ballistic range scales exactly r{rank}/{sling}/{angle}/{height}");
        }
    }
}
Near(SkillMath.ThrowMultiplier(99, true), 10, "rank clamps at four instead of granting a fifth effect");
Near(SkillMath.BulkUp(-1), 1, "negative rank contributes no health modifier");
var gravityOverride = ThrowPhysics.Extend(12, 45, 4);
Near(ThrowPhysics.HorizontalRange(gravityOverride.Force, gravityOverride.AngleDegrees, 1.5, 3),
    4 * ThrowPhysics.HorizontalRange(12, 45, 1.5, 3), "arc and landing marker share weapon-specific gravity rather than default-world gravity");
Near(SkillMath.DirectDamage(100, true, .8, 100, 100, 2, 4, 3, 4, true, 1.3, 25), 1136.5,
    "independent direct-hit multipliers stack multiplicatively and Payback adds once afterward");
Near(SkillMath.DirectDamage(10, true, .1, 90, 100, 4, 3, 4, 0, false, 1, 0), 20.5,
    "low-stamina melee uses the original pre-payment snapshot without full-health bonus");
Near(SkillMath.DirectDamage(100, false, .8, 100, 100, 4, 4, 1, 0, false, 1, 25), 130,
    "ranged direct damage excludes melee-only thresholds and Payback reservoir");
Near(OwnershipDelta.Clamp(4, 1, 4, true), 0, "canonical custom acquisition refuses rank five");
Near(OwnershipDelta.Clamp(0, 1, 1, true), 1, "vanilla-style one-rank cap remains independent");
// Changing sources advances only future clock steps; existing progress is not divided/rebased.
Near(.5 + .1 + ThrowPhysics.ExtraClockStep(.1, SkillMath.ThrowMultiplier(1, true)), .9, "equipping sling changes next clock step without instant completion");
Near(.5 + .1 + ThrowPhysics.ExtraClockStep(.1, SkillMath.ThrowMultiplier(0, false)), .6, "removing all sources retains earned progress and removes only future speed");

foreach (var entry in SkillDefinitions.All)
for (var rank = 0; rank <= 4; rank++)
{
    var text = SkillMath.Description(entry.Kind, rank);
    Check(text == SkillMath.Effect(entry.Kind, rank) && text.Length <= 125 && !text.Contains('\n') &&
        SkillMath.OfferDescription(entry.Kind, rank) == SkillMath.Effect(entry.Kind, Math.Min(4,rank+1)),
        $"short owned-rank text and next-rank offer for {entry.Name} rank {rank}");
}

var state = new SkillCombatState(0);
Check(!state.Commit(1.999, 1) && state.Commit(4, 4) && !state.Commit(4.01, 4), "one Pick charge after two seconds, consumed on attempt including miss");
var melee = state.Plan("melee", HitKind.Melee, 4.02, 2, 0, 100); state.Resolve(melee, true, 4.02, 2);
var thrown = state.Plan("throw", HitKind.ThrownWeapon, 5, 2, 0, 100);
Near(thrown.Multiplier, 1.6, "melee success primes next thrown weapon hit");
var duplicate = state.Plan("another", HitKind.ThrownWeapon, 5, 2, 0, 100);
Near(duplicate.Multiplier, 1, "pending hit reserves the sole mixed charge");
state.Resolve(thrown, false, 5, 2);
var retry = state.Plan("retry", HitKind.ThrownWeapon, 5.1, 2, 0, 100);
Near(retry.Multiplier, 1.6, "blocked/zero-HP hit releases reservation without consuming mixed charge");
state.Resolve(retry, true, 5.1, 2); state.Resolve(retry, true, 5.1, 2);
var nextMelee = state.Plan("next-melee", HitKind.Melee, 5.2, 2, 0, 100);
Near(nextMelee.Multiplier, 1.6, "thrown success primes opposite kind, duplicate resolution does not stack");
state.Resolve(nextMelee, true, 5.2, 2);
var expired = state.Plan("expired", HitKind.ThrownWeapon, 11.2, 2, 0, 100);
Near(expired.Multiplier, 1, "mixed charge expires at six seconds");
state.Resolve(expired, false, 11.2, 2);

var reservoir = new SkillCombatState(0);
reservoir.EnemyDamage(0, 1, 10, 100); reservoir.EnemyDamage(1, 4, 1000, 100);
var held = reservoir.Plan("held", HitKind.Melee, 2, 0, 4, 100);
Near(held.Payback, 25, "actual loss fills fixed reservoir cap without overkill amplification");
Reject(() => reservoir.Capture(2, 100), "checkpoint refuses unresolved resource reservations rather than replaying them");
Near(reservoir.Plan("parallel", HitKind.Melee, 2, 0, 4, 100).Payback, 0, "parallel/nested candidate cannot spend same reservoir twice");
reservoir.Resolve(held, false, 2, 0);
var successful = reservoir.Plan("success", HitKind.Melee, 2.1, 0, 4, 100); reservoir.Resolve(successful, true, 2.1, 0);
reservoir.Resolve(successful, true, 2.1, 0);
Near(reservoir.Plan("after", HitKind.Melee, 2.2, 0, 4, 100).Payback, 0, "successful direct melee consumes reservoir exactly once");
reservoir.EnemyDamage(3, 4, 10, 100); reservoir.EnemyDamage(7.9, 1, 1, 100);
var saved = reservoir.Capture(8, 100); var json = JsonSerializer.Serialize(saved);
var restored = SkillCombatState.Restore(JsonSerializer.Deserialize<SkillStateSnapshot>(json), 1000);
var afterReload = restored.Plan("restored", HitKind.Melee, 1000, 0, 1, 40);
Near(afterReload.Payback, 10, "restored reservoir preserves remaining time and clamps to changed max HP");
restored.Resolve(afterReload, false, 1000, 0);
Near(restored.Plan("timed-out", HitKind.Melee, 1004.9, 0, 1, 40).Payback, 0, "latest qualifying hit refreshes five-second expiry across checkpoint clock reset");
reservoir.OwnershipChanged(0, 0);
Near(reservoir.Plan("removed", HitKind.Melee, 8, 0, 4, 100).Payback, 0, "normal rank removal clears future stored bonus");
Reject(() => SkillCombatState.Restore(new SkillStateSnapshot { SchemaVersion = 999 }, 0), "newer skill-state schema explicitly rejected during preflight");
Reject(() => SkillCombatState.Restore(new SkillStateSnapshot { Reservoir = double.NaN }, 0), "malformed timer/reservoir state cannot enter gameplay");
var emptyState = new SkillCombatState(1000); Check(!emptyState.Commit(1000, 4), "fresh battle/run has no inherited pending readiness");
Check(new SkillCombatState(0).Commit(2, 1), "first attack after two seconds in battle can be empowered");

var attempts = new AttackAttemptLedger<object>(); var commits = 0;
var first = attempts.GetOrCreate(0, "native-timestamp:weapon:flurry", () => { commits++; return new object(); }, out _);
var second = attempts.GetOrCreate(0, "native-timestamp:weapon:flurry", () => { commits++; return new object(); }, out _);
Check(ReferenceEquals(first, second) && commits == 1, "base/override/cost duplicate callbacks share one committed attack");
attempts.GetOrCreate(0, "next-timestamp:weapon:flurry", () => { commits++; return new object(); }, out _);
Check(commits == 2, "next real native attempt commits separately even after a miss");
Check(DurabilityTransition.IsBreak(1, 0, 1, true, false) && !DurabilityTransition.IsBreak(0, 0, 1, true, false), "only positive-to-broken normal loss triggers Waste Not");
Check(DurabilityTransition.IsBreak(1, 1, 1, true, false), "effective early durability break confirmed by native destroy path");
Check(!DurabilityTransition.IsBreak(2, 2, 1, false, false) && !DurabilityTransition.IsBreak(2, 2, 0, true, false) &&
    !DurabilityTransition.IsBreak(1, 0, 1, true, true), "ordinary durability use, drops and default weapons do not reward a break");

var repository = Path.GetFullPath(args[0]);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "docs/terms-and-contusions/manifest.json")));
var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
var stage = manifest.RootElement.GetProperty("stage").GetInt32();
Check(entries.Count(value => value.GetProperty("implementationStatus").GetString() == (stage >= 7 ? "implemented-runtime-unverified" : "implemented-build-only")) == (stage >= 7 ? 46 : stage >= 6 ? 44 : stage >= 5 ? 33 : stage >= 4 ? 23 : 13) &&
    entries.Count(value => value.GetProperty("implementationStatus").GetString() == "planned") == (stage >= 6 ? 0 : stage >= 5 ? 10 : stage >= 4 ? 20 : 30), "catalog accounting matches the current stage boundary");
foreach (var entry in SkillDefinitions.All)
{
    var item = entries.Single(value => value.GetProperty("key").GetString() == entry.Key);
    Check(item.GetProperty("numericId").GetInt32() == entry.Id && item.GetProperty("implementedMaxRank").GetInt32() == 4, "manifest maps actual four-rank definition " + entry.Name);
}
using var gamePe = new PEReader(File.OpenRead(Path.Combine(args[1], "BepInEx/interop/Assembly-CSharp.dll")));
var metadata = gamePe.GetMetadataReader();
TypeDefinition Type(string name) => metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle)).Single(type => metadata.GetString(type.Name) == name);
foreach (var expected in new[] {
    ("Player", "ConsumeMeleeStamina", 1), ("Player", "ModifyAttackDamage", 7), ("Player", "UpdateStamina", 0),
    ("Player", "FindTargetAgent", 4), ("NetworkAgent", "ExecuteThrow", 2), ("NetworkAgent", "LaunchThrowProjectileRPC", 17),
    ("NetworkAgent", "LaunchThrowPostDelay", 16), ("NetworkAgent", "DestroyWeapon", 2),
    ("_RunWindupLoop_d__443", "MoveNext", 0), ("_LaunchThrowPostDelay_d__476", "MoveNext", 0),
    ("Projectile", "Launch", 15), ("Projectile", "DealDamage", 4), ("Projectile", "OnDestroy", 0),
    ("NetworkHealthObject", "TakeDamage", 8), ("NetworkHealthObject", "TakeDamageRpc", 8), ("Weapon", "LoseDurability", 1),
    ("WeaponFlail", "PerformCollision", 2), ("TelegrapherArc", "SetScale", 1), ("TelegrapherArc", "SetLineRenderer", 4),
    ("UILobbyTrainerUpgradePage", "Refresh", 4), ("UILobbyTrainerUpgradePage", "SetSkill", 2), ("UIGameLevelLobbyCharacterSkillInfo", "Initialise", 2)
})
{
    var methods = Type(expected.Item1).GetMethods().Select(handle => metadata.GetMethodDefinition(handle));
    Check(methods.Count(method => metadata.GetString(method.Name) == expected.Item2 &&
        method.GetParameters().Count(handle => metadata.GetParameter(handle).SequenceNumber > 0) == expected.Item3) == 1,
        $"installed unique Stage 3 target {expected.Item1}.{expected.Item2}/{expected.Item3}");
}
foreach (var claim in SkillDefinitions.All.Select(value => ("SkillType", value.Id)).Append(("PurchaseType", 2498)))
{
    var numbers = Type(claim.Item1).GetFields().Select(handle => metadata.GetFieldDefinition(handle).GetDefaultValue()).Where(handle => !handle.IsNil)
        .Select(handle => metadata.GetBlobReader(metadata.GetConstant(handle).Value).ReadInt32());
    Check(!numbers.Contains(claim.Item2), $"installed native enum leaves {claim.Item1} {claim.Item2} unclaimed");
}
Console.WriteLine($"Stage 3: {passed} production-logic/metadata checks passed. No Unity/gameplay initialized.");
