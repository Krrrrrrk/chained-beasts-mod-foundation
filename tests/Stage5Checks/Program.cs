using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TermsAndContusions.Afflictions;
using ChainedBeasts.ModFoundation.Stats;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
void Near(double actual, double expected, string name, double tolerance = 1e-8) => Check(Math.Abs(actual - expected) <= tolerance, name);
void Reject(Action callback, string name) { try { callback(); } catch (InvalidOperationException) { Check(true, name); return; } throw new Exception("FAIL accepted: " + name); }
AfflictionBattleState RoundTrip(AfflictionBattleState state, double saveTime, double restoreTime) => AfflictionBattleState.Restore(JsonSerializer.Deserialize<AfflictionSnapshot>(JsonSerializer.Serialize(state.Capture(saveTime))), restoreTime);

Check(AfflictionDefinitions.All.Length == 10 && AfflictionDefinitions.All.Select(value => value.Key).Distinct().Count() == 10 &&
    AfflictionDefinitions.All.Select(value => value.Id).Distinct().Count() == 10, "ten independent explicit affliction identities beside preserved Leadfoot");
var expectedRecovery = new[] { 1d, .8d, .65d, .5d };
var expectedLanding = new[] { 1d, .8d, .7d, .6d };
var expectedFocus = new[] { 1d, .85d, .75d, .65d };
for (var severity = 0; severity <= 3; severity++)
{
    Near(AfflictionMath.LeadDelay(severity), .25 * severity, "Lead Belly exact delay " + severity);
    Check(AfflictionMath.BlockInterval(severity) == (severity == 0 ? 0 : 5 - severity), "Brittle interval " + severity);
    Near(AfflictionMath.ShakyDrain(severity, 100, 100), 5 * severity, "Shaky exact max-stamina fraction " + severity);
    Near(AfflictionMath.ShakyDrain(severity, 100, 2), severity == 0 ? 0 : 2, "Shaky cannot overdraw current stamina " + severity);
    Near(AfflictionMath.Favour(severity), 1 - .15 * severity, "Tin Ear factor " + severity);
    Near(AfflictionMath.Recovery(severity, true), expectedRecovery[severity], "moving Burning Lungs " + severity);
    Near(AfflictionMath.Recovery(severity, false), 1, "stationary recovery neutral " + severity);
    Near(AfflictionMath.Landing(severity, true), expectedLanding[severity], "Heavy Landing factor " + severity);
    Near(AfflictionMath.Landing(severity, false), 1, "no landing charge neutral " + severity);
    Check(AfflictionMath.GoldLoss(severity, 100) == severity && AfflictionMath.GoldLoss(severity, 1) == Math.Min(1, severity), "Loose Change exact/clamped gold " + severity);
    Near(AfflictionMath.Cold(severity, 0), 1 - .1 * severity, "Cold Start at battle beginning " + severity);
    Near(AfflictionMath.Cold(severity, 9.999), 1 - .1 * severity, "Cold Start before ten seconds " + severity);
    Near(AfflictionMath.Cold(severity, 10), 1, "Cold Start ten-second boundary " + severity);
    Near(AfflictionMath.Focus(severity), expectedFocus[severity], "Focus exact factor " + severity);
    for (var stacks = 0; stacks <= 3; stacks++) Near(AfflictionMath.MeleeCost(severity, stacks), 1 + .05 * severity * stacks, "Overheat severity/stack " + severity + "/" + stacks);
    foreach (var entry in AfflictionDefinitions.All)
        Check(AfflictionDefinitions.Find(entry.Id) == entry && AfflictionDefinitions.Get(entry.Kind) == entry && AfflictionMath.Description(entry.Kind, severity).Length > 20,
            "severity text/identity " + entry.Name + "/" + severity);
}
Near(AfflictionMath.LeadDelay(-1), 0, "negative effective severity neutral");
Near(AfflictionMath.LeadDelay(99), .75, "severity capped at three");
Check(AfflictionMath.GoldLoss(3, 0) == 0 && AfflictionMath.GoldLoss(3, -1) == 0, "gold never falls below zero");
Near(AfflictionMath.FavourDelta(100, 120, .55), 111, "Tin Ear scales only new favor contribution");
Near(AfflictionMath.FavourDelta(100, 80, .55), 80, "native negative favor/decay is unchanged");
Near(AfflictionMath.FavourDelta(100, 100, .55), 100, "no favor event cannot compound an old value");
var favor = 100d; for (var i = 0; i < 3; i++) favor = AfflictionMath.FavourDelta(favor, favor + 20, .55);
Near(favor, 133, "separate events each apply one contribution factor without rescaling accumulated favor");
var pipeline = new ModifierPipeline<bool>(); pipeline.Register("pack:burning", StatChannel.RegenerationAmount, moving => AfflictionMath.Recovery(3, moving));
pipeline.Register("another:regeneration", StatChannel.RegenerationAmount, _ => 1.25);
Near(pipeline.Evaluate(StatChannel.RegenerationAmount, 10, true), 6.25, "independent regeneration sources multiply");
Near(pipeline.Evaluate(StatChannel.RegenerationAmount, 10, false), 12.5, "stationary removes only burning contribution");
Near(pipeline.Evaluate(StatChannel.StaminaRecoveryRate, 10, true), 10, "regeneration grant channel does not also multiply base rate getter");
var movement = new ModifierPipeline<bool>();
movement.Register("pack:leadfoot", StatChannel.MovementSpeed, _ => .7);
movement.Register("pack:landing", StatChannel.MovementSpeed, isDashing => isDashing ? 1 : AfflictionMath.Landing(3, true));
Near(movement.Evaluate(StatChannel.MovementSpeed, 10, false), 4.2, "preserved Leadfoot and Heavy Landing multiply only their own contributions");
Near(movement.Evaluate(StatChannel.MovementSpeed, 10, true), 7, "Heavy Landing removes its own modifier during dodge without disabling Leadfoot");

var delay = new ExertionDelayState();
Near(delay.Begin(1, .75), 1.75, "one genuine exertion appends Lead Belly once");
for (var i = 0; i < 4; i++) Near(delay.Reconcile(1.75, .75), 1.75, "refresh does not add another delay " + i);
Near(delay.Reconcile(.5, .75), .5, "native timer consumes normal delay then affliction tail");
Near(delay.Reconcile(.5, .25), 0, "severity reduction removes already elapsed portion, not a fresh .25 delay");
Near(delay.Begin(1, .75), 1.75, "new exertion replaces prior own delay rather than compounding");
Near(delay.Reconcile(1.5, 0), .75, "cure/suppression removes only remaining own delay and retains native remainder");
Near(delay.Reconcile(.75, .75), .75, "unsuppress/reacquire cannot rearm cured exertion without new payment");
Near(delay.Begin(.5, .25), .75, "later actual exertion rearms correct reduced severity");
Near(delay.Reconcile(0, .25), 0, "completed recovery never starts another delay on a read");
Near(delay.Reconcile(0, .75), 0, "severity increase after completed recovery cannot replay delay");
var stackedDelay = new ExertionDelayState(); var totalDelay = stackedDelay.Begin(1, .5);
Near(totalDelay / 5, .3, "Lead Belly additive delay and rank-four Catch clock compose once");
Near(stackedDelay.Reconcile(.4, .5), .4, "accelerated native clock may consume own tail normally");
Near(stackedDelay.Reconcile(.4, 0), 0, "cure during accelerated tail does not restore consumed time");

for (var severity = 1; severity <= 3; severity++)
{
    var state = new AfflictionBattleState(0); var period = AfflictionMath.BlockInterval(severity);
    for (var hit = 1; hit <= 12; hit++) Check(state.Block(severity) == (hit % period == 0), "real shield-hit counter " + severity + "/" + hit);
}
var counter = new AfflictionBattleState(0); counter.Block(1); counter.Block(1); counter.Block(1); counter.Sync(3, 0, 0, 0);
Check(counter.Block(3), "lower block interval clamps accrued count and applies at next real block, not severity change");
counter.Block(1); counter.Sync(0, 0, 0, 0);
Check(!counter.Block(1), "suppression/cure clears partial brittle counter");
counter.Block(1); var counterReload = RoundTrip(counter, 1, 1000);
Check(!counterReload.Block(1) && counterReload.Block(1), "partial block count round trips independently");

var damage = new AfflictionBattleState(0);
Check(!damage.Shaky(0, 3, 0) && !damage.Loose(0, 3, 0), "attempted/blocked damage starts no damage cooldown");
Check(damage.Shaky(0, 3, 1) && damage.Loose(0, 3, 1), "first actual enemy loss triggers independent penalties");
Check(!damage.Shaky(0, 3, 1) && !damage.Loose(0, 3, 1), "duplicate callback cannot repeat penalties");
Check(!damage.Shaky(.999, 3, 1) && damage.Shaky(1, 1, 1), "Shaky fixed one-second boundary independent of severity");
Check(!damage.Loose(2.999, 3, 1) && damage.Loose(3, 1, 1), "Loose Change fixed three-second boundary independent of severity");
damage = RoundTrip(damage, 3.5, 1000);
Check(damage.Shaky(1000, 1, 1) && !damage.Loose(1002.49, 1, 1) && damage.Loose(1002.5, 3, 1), "cooldown remaining time survives process clock reset");
Check(!damage.Shaky(1005, 0, 1) && !damage.Loose(1005, 0, 1), "cured/suppressed penalty is neutral");

var landing = new AfflictionBattleState(0); landing.DodgeCompleted(5, 3);
Near(landing.Movement(5.749, 3), .6, "landing fixed .75-second window");
Near(landing.Movement(5.75, 3), 1, "landing expires at boundary");
landing.DodgeCompleted(6, 3); landing.Sync(0, 1, 0, 0);
Near(landing.Movement(6.1, 1), .8, "pending landing uses reduced effective severity");
landing = RoundTrip(landing, 6.25, 1000); Near(landing.Movement(1000.499, 1), .8, "landing remaining time restored");
Near(landing.Movement(1000.5, 1), 1, "landing does not gain a fresh duration on restore");
landing.DodgeCompleted(1001, 3); landing.Sync(0, 0, 0, 0);
Near(landing.Movement(1001.1, 3), 1, "cure clears old landing charge even after later reacquisition");
var cold = RoundTrip(new AfflictionBattleState(0), 7, 1000);
Near(AfflictionMath.Cold(3, cold.BattleAge(1000)), .7, "Cold Start preserves elapsed battle window");
Near(AfflictionMath.Cold(3, cold.BattleAge(1003)), 1, "Cold Start is not a new ten-second debuff after load");

var heat = new AfflictionBattleState(0);
for (var i = 0; i < 6; i++)
{
    var attack = heat.Commit(i * .2, true, 3);
    Near(heat.MeleeMultiplier(attack, 3), 1 + .15 * Math.Min(i, 3), "consecutive committed attack cost " + i);
    Near(heat.MeleeMultiplier(attack, 3), 1 + .15 * Math.Min(i, 3), "cost read/secondary payment does not add a stack " + i);
}
Near(heat.MeleeMultiplier(heat.Commit(3, true, 3), 3), 1, "two seconds since prior commit clears heat before this payment");
heat.Commit(3.1, true, 3); var pendingHeat = heat.Commit(3.2, true, 3); heat.Sync(0, 0, 0, 0);
Near(heat.MeleeMultiplier(pendingHeat, 3), 1, "cure/suppression invalidates cached pre-payment heat even after reacquisition");
Near(heat.MeleeMultiplier(heat.Commit(3.3, true, 3), 3), 1, "first attack after removal starts fresh");
heat.Commit(3.4, true, 3); heat.Commit(3.5, false, 3);
Near(heat.MeleeMultiplier(heat.Commit(3.6, true, 3), 3), 1, "ranged attempt breaks consecutive melee chain");
heat.Commit(3.7, true, 3); heat = RoundTrip(heat, 4, 1000);
Near(heat.MeleeMultiplier(heat.Commit(1000.1, true, 2), 2), 1.2, "heat count/attack age restore with current effective severity");
Near(heat.MeleeMultiplier(heat.Commit(1002.1, true, 2), 2), 1, "restored heat clears after fixed idle gap");

AfflictionBattleState FocusSeed()
{ var state = new AfflictionBattleState(0); state.SuccessfulDirectHit(new AfflictionAttack(), "old", 50, 1, 3, 1); return state; }
var focus = FocusSeed(); var multi = new AfflictionAttack();
Near(focus.FocusMultiplier(multi, "old", 2, 3), 1, "focus evaluates old target before updating");
focus.SuccessfulDirectHit(multi, "b", 20, 2, 3, 1);
Near(focus.FocusMultiplier(multi, "a", 2, 3), .65, "all targets in same action use previous focus");
focus.SuccessfulDirectHit(multi, "a", 10, 2, 3, 1);
Check(focus.FocusTarget == "a", "next focus chooses smallest native target order among successful targets");
var reverse = FocusSeed(); var reverseAction = new AfflictionAttack(); reverse.FocusMultiplier(reverseAction, "a", 2, 3);
reverse.SuccessfulDirectHit(reverseAction, "a", 10, 2, 3, 1); reverse.SuccessfulDirectHit(reverseAction, "b", 20, 2, 3, 1);
Check(reverse.FocusTarget == focus.FocusTarget, "multi-target callback order cannot change chosen focus");
Near(reverse.FocusMultiplier(reverseAction, "old", 2, 3), 1, "same action cannot accidentally penalize original focus after first callback");
var blockedFocus = new AfflictionAttack(); focus.FocusMultiplier(blockedFocus, "b", 2.1, 3); focus.SuccessfulDirectHit(blockedFocus, "b", 1, 2.1, 3, 0);
Check(focus.FocusTarget == "a", "blocked/zero-HP attempt cannot change focus");
Near(focus.FocusMultiplier(new AfflictionAttack(), "b", 5, 3), 1, "focus expires three seconds after last qualifying direct hit");
var curedFocus = FocusSeed(); var frozen = new AfflictionAttack(); curedFocus.FocusMultiplier(frozen, "other", 1.1, 3); curedFocus.Sync(0, 0, 0, 0);
Near(curedFocus.FocusMultiplier(frozen, "other", 1.2, 3), 1, "cure invalidates cached focus on an in-flight/committed action");
var restoredFocus = RoundTrip(FocusSeed(), 2, 1000);
Near(restoredFocus.FocusMultiplier(new AfflictionAttack(), "other", 1001.999, 2), .75, "restored focus preserves remaining time with current severity");
Near(restoredFocus.FocusMultiplier(new AfflictionAttack(), "other", 1002, 2), 1, "restored focus expires without a refreshed duration");
restoredFocus.ClearFocus(); Check(restoredFocus.FocusTarget is null, "unavailable native arena focus can be cleared explicitly without resource replay");
var fresh = new AfflictionBattleState(1000);
Check(fresh.FocusTarget is null && fresh.Shaky(1000, 3, 1) && fresh.Loose(1000, 3, 1), "fresh battle/run has no old focus or penalty cooldown");
Reject(() => AfflictionBattleState.Restore(new AfflictionSnapshot { SchemaVersion = 99 }, 0), "unknown feature schema rejected");
Reject(() => AfflictionBattleState.Restore(new AfflictionSnapshot { ShakyRemaining = 1.1 }, 0), "invalid cooldown rejected");
Reject(() => AfflictionBattleState.Restore(new AfflictionSnapshot { BattleAge = double.NaN }, 0), "nonfinite age rejected");
Reject(() => AfflictionBattleState.Restore(new AfflictionSnapshot { FocusRemaining = 1 }, 0), "missing focus identity rejected");
Reject(() => AfflictionBattleState.Restore(new AfflictionSnapshot { OverheatStacks = 4, LastWasMelee = true }, 0), "over-cap heat rejected");
Reject(() => AfflictionBattleState.Restore(new AfflictionSnapshot { BlockCount = 4 }, 0), "over-cap shield counter rejected");
Reject(() => AfflictionBattleState.Restore(new AfflictionSnapshot { OverheatStacks = 1, LastWasMelee = false }, 0), "inconsistent melee chain rejected");

var root = Path.GetFullPath(args[0]);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/terms-and-contusions/manifest.json")));
var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
var stage = manifest.RootElement.GetProperty("stage").GetInt32();
Check(stage >= 5 && entries.Count(value => value.GetProperty("implementationStatus").GetString() == "planned") == (stage >= 6 ? 0 : 10),
    "Stage 5 complete; later planned entries respect the current boundary");
foreach (var entry in AfflictionDefinitions.All)
{
    var recorded = entries.Single(value => value.GetProperty("key").GetString() == entry.Key);
    Check(recorded.GetProperty("numericId").GetInt32() == entry.Id && recorded.GetProperty("implementedMaxSeverity").GetInt32() == 3 &&
        recorded.GetProperty("implementationStatus").GetString() == (stage >= 7 ? "implemented-runtime-unverified" : "implemented-build-only"), "manifest matches actual affliction " + entry.Name);
}
using var gamePe = new PEReader(File.OpenRead(Path.Combine(args[1], "BepInEx/interop/Assembly-CSharp.dll")));
var metadata = gamePe.GetMetadataReader();
TypeDefinition Type(string name) => metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle)).Single(type => metadata.GetString(type.Name) == name);
foreach (var expected in new[] {
    ("Player", "ConsumeStamina", 3), ("Player", "ConsumeMeleeStamina", 1), ("Player", "UpdateStamina", 0), ("Player", "RestoreStamina", 1),
    ("Player", "HasInjury", 1), ("Player", "StartDash", 0), ("Player", "OnDashComplete", 0),
    ("NetworkHealthObject", "TakeHitRpc", 11), ("NetworkAgent", "OnBlock", 6), ("PlayerCrowdData", "ProcessBattleEvent", 1),
    ("PlayerData", "AddInjury", 2), ("PlayerData", "RemoveInjury", 2), ("PlayerData", "RemoveAllInjuries", 0),
    ("InjuryGenerator", "GeneratePotions", 4), ("RunDataManager", "ConsumePotionRpc", 4),
    ("StringManager", "GetInjuryNameTerm", 1), ("StringManager", "GetInjuryName", 2), ("StringManager", "GetInjuryDescTerm", 2)
})
{
    var methods = Type(expected.Item1).GetMethods().Select(handle => metadata.GetMethodDefinition(handle));
    Check(methods.Count(method => metadata.GetString(method.Name) == expected.Item2 && method.GetParameters().Count(handle => metadata.GetParameter(handle).SequenceNumber > 0) == expected.Item3) == 1,
        $"installed unique Stage 5 path {expected.Item1}.{expected.Item2}/{expected.Item3}");
}
var nativeIds = Type("InjuryType").GetFields().Select(handle => metadata.GetFieldDefinition(handle).GetDefaultValue()).Where(handle => !handle.IsNil)
    .Select(handle => metadata.GetBlobReader(metadata.GetConstant(handle).Value).ReadInt32()).ToArray();
foreach (var entry in AfflictionDefinitions.All) Check(!nativeIds.Contains(entry.Id), "installed native affliction ID vacancy " + entry.Id);
Console.WriteLine($"Stage 5: {passed} production-logic/metadata checks passed. No Unity/gameplay initialized.");
