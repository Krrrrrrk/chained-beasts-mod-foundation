using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.Stats;
using TermsAndContusions.Surgeries;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
void Near(double actual, double expected, string name) => Check(double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-8, name);
void Reject(Action callback, string name) { try { callback(); } catch (InvalidOperationException) { Check(true, name); return; } throw new Exception("FAIL accepted: " + name); }
SurgeryBattleState RoundTrip(SurgeryBattleState state, double save, double resume) => SurgeryBattleState.Restore(JsonSerializer.Deserialize<SurgerySnapshot>(JsonSerializer.Serialize(state.Capture(save))), resume);
Check(SurgeryDefinitions.All.Length == 11 && SurgeryDefinitions.All.Select(value => value.Id).Distinct().Count() == 11 && SurgeryDefinitions.All.Select(value => value.Key).Distinct().Count() == 11,
    "all eleven independent stable surgery identities");
Check(SurgeryDefinitions.Get(CustomSurgery.AdrenalGraft).Id == 11020 && SurgeryDefinitions.Get(CustomSurgery.AdrenalGraft).DefaultPrice == 0, "Adrenal identity/free price preserved");
foreach (var entry in SurgeryDefinitions.All)
    Check(entry.Name.Length > 0 && entry.Description.Length > 10 && (entry.Kind == CustomSurgery.AdrenalGraft || entry.DefaultPrice > 0), "actual text/nonzero new price " + entry.Name);
foreach (var returned in new[] { false, true }) foreach (var completion in new[] { false, true }) foreach (var owned in new[] { false, true }) foreach (var paid in new[] { false, true })
{
    var receipt = new SurgeryCompletion();
    receipt.Observe(false, 35, 1); receipt.Observe(true, 34, 1); receipt.Observe(true, 35, 0);
    if (completion) receipt.Observe(true, 35, 1);
    Check(receipt.Commit(returned, owned, paid) == (returned && completion && !owned && paid), $"completion matrix returned={returned} nativeCompletion={completion} owned={owned} paid={paid}");
    if (returned && completion && !owned && paid) Check(!receipt.Commit(true, false, true), "duplicate completion receipt commits once");
}
var free = new SurgeryCompletion(); Check(!free.Commit(true, false, true), "executing free RPC and unchanged balance alone grant nothing");
free.Observe(true, 35, 1); Check(free.Commit(true, false, true), "free surgery requires genuine native completion and normal return");
foreach (var enabled in new[] { false, true }) foreach (var provider in new[] { false, true }) foreach (var owned in new[] { false, true }) foreach (var nested in new[] { false, true })
    Check(SurgeryEligibility.CanAcquire(enabled, provider, owned, 40, 40, 40, nested) == (enabled && provider && !owned && !nested), "native pre-payment eligibility matrix");
Check(SurgeryEligibility.CanAcquire(true, true, false, 0, 0, 0, false), "free eligible surgery accepted");
Check(!SurgeryEligibility.CanAcquire(true, true, false, 40, 40, 39, false) && !SurgeryEligibility.CanAcquire(true, true, false, 40, 0, 40, false) && !SurgeryEligibility.CanAcquire(true, true, false, -1, -1, 40, false), "insufficient gold, changed quote and negative price rejected before payment");

var seen = new HashSet<int>();
for (var seed = 0; seed < 64; seed++)
{
    var selected = SurgeryOffers.Select(3, 11, 3, seed);
    Check(selected.Length == 3 && selected.Distinct().Count() == 3 && selected.All(value => value >= 0 && value < 14), "combined pool no replacement/count bound seed " + seed);
    Check(selected.SequenceEqual(SurgeryOffers.Select(3, 11, 3, seed)), "same visit deterministic; no Unity RNG disturbance " + seed);
    foreach (var choice in selected) seen.Add(choice);
}
Check(seen.Count == 14, "every native and custom candidate can participate");
Check(SurgeryOffers.Select(0, 0, 4, 3).Length == 0 && SurgeryOffers.Select(3, 11, 0, 3, 0).Length == 0, "empty/protected slots never create extra options");
Check(SurgeryOffers.Select(3, 11, 3, 3, 0)[0] == 3 && SurgeryOffers.Select(3, 11, 3, 3, 0).Distinct().Count() == 3, "legacy force override selects Adrenal once from shared pool");
Check(SurgeryOffers.Select(4, 0, 4, 3).OrderBy(value => value).SequenceEqual(new[] { 0, 1, 2, 3 }), "no eligible custom providers retains native pool");
Check(SurgeryOffers.Select(0, 11, 4, 3).Length == 4 && SurgeryOffers.Select(0, 1, 4, 3).Length == 1, "exhausted native pool can fill requested surgeon slots with distinct eligible enhancements");

var pipeline = new ModifierPipeline<bool>();
foreach (var entry in SurgeryDefinitions.All) foreach (var channel in Enum.GetValues<StatChannel>())
{
    var kind = entry.Kind; var selectedChannel = channel;
    pipeline.Register(entry.Key + "/" + channel, channel, owned => owned ? SurgeryMath.Factor(kind, selectedChannel) : 1);
}
Near(pipeline.Evaluate(StatChannel.MaximumHealth, 100, true), 76.5, "Tendons x Marrow from fresh native max");
Near(pipeline.Evaluate(StatChannel.MaximumHealth, 100, true), 76.5, "recalculation never multiplies modified value again");
Near(pipeline.Evaluate(StatChannel.MaximumStamina, 100, true), 72, "Dense x Pain maximum stamina");
Near(pipeline.Evaluate(StatChannel.DodgeDistance, 10, true), 10.4, "Anchor x Spring dodge distance");
Near(pipeline.Evaluate(StatChannel.DodgeStaminaCost, 10, true) * .75, 9, "Spring x Runner's Wraps normal cost factors");
Near(pipeline.Evaluate(StatChannel.StaminaRecoveryRate, 10, true), 12.5, "preserved Adrenal recovery");
Near(pipeline.Evaluate(StatChannel.HealingAmount, 10, true), 13, "Marrow healing before native clamp");
Near(pipeline.Evaluate(StatChannel.IncomingKnockback, 10, true), 2.5, "Anchor native distance reduction");
Near(pipeline.Evaluate(StatChannel.MaximumHealth, 100, false), 100, "unowned/disabled native values unchanged");
Near(pipeline.Evaluate(StatChannel.MovementSpeed, 10, true), 10, "no unintended ordinary movement change");
pipeline.Register("test:bulk", StatChannel.MaximumHealth, owned => owned ? 3 : 1);
var maximum = (int)Math.Round(pipeline.Evaluate(StatChannel.MaximumHealth, 100, true));
Check(maximum == 230 && HealthTransition.Scale(180, 300, maximum, maximum) == 138, "Bulk rank two x health surgeries one proportional current-HP transition");
Check(HealthTransition.Scale(0, 300, maximum, maximum) == 0 && HealthTransition.Scale(180, 300, maximum, 100) == 100, "health surgery never revives and obeys attainable cap");
Near(10 * SurgeryMath.Factor(CustomSurgery.MarrowEnrichment, StatChannel.HealingAmount), 13, "Apron native healing benefits from Marrow exactly once");
Near(SurgeryMath.RegularMelee(true, false) * 1.3, 1.04, "Tendons regular payment x Overheating");
Near(SurgeryMath.RegularMelee(true, true) * 1.3, 1.3, "Tendons never discounts special melee");
Near(SurgeryMath.WeaponDamage(true, true), 1, "Talons unarmed hit unreduced");
Near(SurgeryMath.WeaponDamage(true, false), .9, "Talons armed damage reduced");
Near(SurgeryMath.WeaponDamage(false, false), 1, "unowned weapon damage normal");
for (var loss = 0; loss <= 100; loss += 5)
    Near(SurgeryMath.Pain(loss, 100, 72), Math.Min(loss * .015, .25) * 72, "actual loss fraction and cap " + loss);
Near(SurgeryMath.Pain(5, 100, 100), 7.5, "five percent HP loss becomes seven point five percent stamina");
Near(SurgeryMath.Pain(10, 0, 100), 0, "uninitialized maximum safe");

var state = new SurgeryBattleState();
Check(!state.Clot(true, 19, 100, true, 0) && !state.Clot(true, 20, 100, false, 0) && !state.Clot(false, 20, 100, true, 0), "Clot rejects below threshold, dead and unowned");
Check(state.Clot(true, 20, 100, true, 0), "Clot inclusive twenty-percent actual loss threshold");
Check(state.ClotTick(0) == 0 && state.ClotTick(.999) == 0, "Clot no immediate heal");
Check(state.ClotTick(1) == 1 && state.ClotTick(1) == 0 && state.ClotTick(2) == 1, "Clot progressive integer healing and duplicate tick idempotence");
state = RoundTrip(state, 2.5, 100);
Check(state.ClotTick(100) == 0 && state.ClotTick(100.5) == 1 && state.ClotTick(101.5) == 2 && state.ClotTick(200) == 0, "Clot resumes remaining budget only, total five over four seconds");
Check(!state.Clot(true, 100, 100, true, 200), "Clot once per battle survives checkpoint");
state.ResetBattle(); Check(state.Clot(true, 40, 100, true, 201) && state.ClotTick(205) == 10, "new battle re-arms, delayed tick bounded to exact total");
state.DiscardLiveEffects(); Check(state.ClotTick(300) == 0 && !state.Clot(true, 40, 100, true, 300), "death/leave battle discards tail without rearming same battle");

state = new SurgeryBattleState();
Check(!state.ReserveCounterweight("a", true, 0), "no synthetic recovery on fresh player");
state.Recovery(true, true, 0); state.Recovery(false, true, 1, false);
Check(!state.ReserveCounterweight("a", true, 1), "Trip recovery grace waits for native control recovery");
state.Recovery(false, true, 2); Check(state.ReserveCounterweight("a", true, 2), "completed stun/trip recovery primes five-second charge");
Check(!state.ReserveCounterweight("b", true, 2) && !state.ResolveCounterweight("b", 1), "multi-target callback cannot steal pending charge");
Reject(() => state.Capture(2), "unsettled contact blocks checkpoint");
Check(!state.ResolveCounterweight("a", 0) && state.ReserveCounterweight("b", true, 3), "failed hit releases reservation without spending charge");
Check(state.ResolveCounterweight("b", 1) && !state.ResolveCounterweight("b", 1) && !state.ReserveCounterweight("c", true, 3), "actual hit consumes once; duplicate receipt never rearms");
state.Recovery(false, true, 4); Check(!state.ReserveCounterweight("c", true, 4), "repeated recovered tick grants no further charge");
state.Recovery(true, true, 4); state.Recovery(false, true, 5); state = RoundTrip(state, 6, 100);
Check(state.ReserveCounterweight("d", true, 103.9), "remaining counterweight duration resumes"); state.ResolveCounterweight("d", 0);
Check(!state.ReserveCounterweight("d", true, 104), "counterweight exact five-second expiry");
state.RebaseIncapacitation(false); state.Recovery(false, true, 200); Check(!state.ReserveCounterweight("new", true, 200), "restored native recovery state cannot synthesize new charge");

state = new SurgeryBattleState();
Check(state.Fury(true, 0) && !state.Fury(true, 0) && !state.Fury(true, 7.999), "Fury converts once, prevents duplicate/cooldown grants");
state.ResetBattle(); Check(!state.Fury(true, 1), "new battle does not reset run-level Fury cooldown");
state = RoundTrip(state, 2, 100);
Check(!state.Fury(true, 100) && !state.Fury(true, 105.9) && state.Fury(true, 106), "Fury remaining cooldown checkpointed without resource replay");
Check(!state.Fury(false, 200) && new SurgeryBattleState().Fury(true, 0), "unowned bypass and fresh-run state independent");

state = new SurgeryBattleState(); state.BleedHit("enemy", 0);
Check(state.BleedTick(0).Count == 0 && state.BleedTick(.599).Count == 0, "Talons no immediate damage tick");
var total = 0;
for (var i = 1; i <= 5; i++) { var damage = state.BleedTick(i * .6); total += damage.GetValueOrDefault("enemy"); Check(damage.GetValueOrDefault("enemy") == 1, "Talons cadence tick " + i); }
Check(total == 5 && !state.HasBleeds && state.BleedTick(4).Count == 0, "single Talons application exactly five damage over three seconds");
state.BleedHit("enemy", 10); Check(state.BleedTick(10.6)["enemy"] == 1, "first bleed tick before refresh");
state.BleedHit("enemy", 10.7); Check(state.BleedTick(10.7).Count == 0 && state.Capture(10.7).Bleeds.Length == 1, "repeat hit refreshes one pair without immediate tick/parallel instance");
Check(state.BleedTick(11.2)["enemy"] == 1, "refresh preserves existing tick cadence");
state.BleedHit("second", 11.3); state = RoundTrip(state, 11.4, 100);
Check(state.BleedTick(100).Count == 0 && state.BleedTick(100.4)["enemy"] == 1 && state.BleedTick(100.5)["second"] == 1, "independent target timers resume without immediate replay");
state.RemoveBleed("enemy"); Check(state.Capture(100.5).Bleeds.All(value => value.Target != "enemy"), "dead/despawned target explicitly removed");
state.DiscardLiveEffects(); Check(state.BleedTick(200).Count == 0, "battle end/death clears all bleeds");
state.BleedHit("lag", 0); Check(state.BleedTick(100)["lag"] == 5 && !state.HasBleeds, "large frame gap bounded to remaining five ticks");
var otherAttacker = new SurgeryBattleState(); otherAttacker.BleedHit("same", 0); state.BleedHit("same", 0);
Check(otherAttacker.BleedTick(.6)["same"] == 1 && state.BleedTick(.6)["same"] == 1, "attacker-target pairs independently owned");
var dispatch = new DamageReceiptQueue<string>();
dispatch.Enqueue("attacker:target", "direct"); dispatch.Enqueue("attacker:target", null); dispatch.Enqueue("attacker:target", "execution"); dispatch.Enqueue("other", "different");
Check(dispatch.Count == 4 && dispatch.TryDequeue("attacker:target", out var contact) && contact == "direct", "ordered direct receipt settles its own contact");
Check(dispatch.TryDequeue("attacker:target", out contact) && contact is null, "generated/unrelated RPC consumes its own null contact, never another pending hit");
Check(dispatch.TryDequeue("other", out contact) && contact == "different" && dispatch.TryDequeue("attacker:target", out contact) && contact == "execution", "routes and execution provenance independent");
Check(!dispatch.TryDequeue("attacker:target", out contact) && dispatch.Count == 0, "duplicate receipt cannot consume a second token");
dispatch.Enqueue("attacker:target", "old"); dispatch.Clear(); Check(dispatch.Count == 0 && !dispatch.TryDequeue("attacker:target", out contact), "battle/run reset clears RPC dispatch tokens");
foreach (var invalid in new[] {
    new SurgerySnapshot { SchemaVersion = 2 }, new SurgerySnapshot { FuryRemaining = double.NaN }, new SurgerySnapshot { CounterweightRemaining = 5.01 },
    new SurgerySnapshot { ClotUsed = true, ClotTotal = 10, ClotPaid = 3, ClotTicks = 1 }, new SurgerySnapshot { ClotTotal = 1 },
    new SurgerySnapshot { Bleeds = null }, new SurgerySnapshot { Bleeds = new[] { new BleedSnapshot { Target = "x", Remaining = 4, NextTick = .1 } } },
    new SurgerySnapshot { Bleeds = new[] { new BleedSnapshot { Target = "x", Remaining = 1, NextTick = .1 }, new BleedSnapshot { Target = "x", Remaining = 1, NextTick = .1 } } }
}) Reject(() => SurgeryBattleState.Restore(invalid, 0), "malformed/unsupported checkpoint rejected");

var root = Path.GetFullPath(args[0]);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/terms-and-contusions/manifest.json")));
var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
Check(manifest.RootElement.GetProperty("stage").GetInt32() >= 6 && entries.Length == 46 && entries.All(value => value.GetProperty("implementationStatus").GetString() != "planned"), "Stage 6 all 46 entries accounted for, no planned placeholders");
foreach (var entry in SurgeryDefinitions.All)
{
    var row = entries.Single(value => value.GetProperty("key").GetString() == entry.Key);
    Check(row.GetProperty("numericId").GetInt32() == entry.Id && row.GetProperty("price").GetInt32() == entry.DefaultPrice && row.GetProperty("implementationStatus").GetString() == (manifest.RootElement.GetProperty("stage").GetInt32() >= 7 ? "implemented-runtime-unverified" : "implemented-build-only"),
        "manifest identity/price matches actual surgery " + entry.Name);
}
using var gamePe = new PEReader(File.OpenRead(Path.Combine(args[1], "BepInEx/interop/Assembly-CSharp.dll")));
var metadata = gamePe.GetMetadataReader();
TypeDefinition Type(string name) => metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle)).Single(type => metadata.GetString(type.Name) == name);
foreach (var expected in new[] {
    ("SurgeryGenerator", "GetSurgeries", 7), ("RunDataManager", "PerformSurgeryRpc", 3), ("StringManager", "GetSurgeryName", 1), ("StringManager", "GetSurgeryDesc", 5),
    ("PlayerData", "get_MaxHealth", 0), ("Player", "get_StaminaMax", 0), ("Player", "get_StaminaCostDash", 0), ("Player", "GetVelocity", 1), ("Player", "ConsumeMeleeStamina", 1),
    ("NetworkAgent", "TryKnockback", 2), ("NetworkHealthObject", "HealRpc", 1), ("RunDataManager", "HealRpc", 2), ("RunDataManager", "GetPotionHealAmount", 2),
    ("GladiatorConsts", "SurgeriesHealAmount", 1), ("NetworkAgentEffect`1", "ModifyTimedEffect", 2), ("NetworkAgentEffect`1", "ModifyEffectStack", 2),
    ("NetworkAgent", "IsEffectActive", 1), ("NetworkAgent", "get_IsIncapacitated", 0), ("Data", "GetSurgeryCodex", 1),
    ("Weapon", "get_WeaponClass", 0), ("NetworkHealthObject", "TakeDamage", 8)
})
{
    var methods = Type(expected.Item1).GetMethods().Select(handle => metadata.GetMethodDefinition(handle));
    Check(methods.Count(method => metadata.GetString(method.Name) == expected.Item2 && method.GetParameters().Count(handle => metadata.GetParameter(handle).SequenceNumber > 0) == expected.Item3) == 1,
        $"installed unique Stage 6 path {expected.Item1}.{expected.Item2}/{expected.Item3}");
}
var award = Type("AwardType").GetFields().Select(handle => metadata.GetFieldDefinition(handle)).Single(field => metadata.GetString(field.Name) == "MostSurgeries");
Check(metadata.GetBlobReader(metadata.GetConstant(award.GetDefaultValue()).Value).ReadInt32() == 35, "native MostSurgeries completion counter is 35");
var awardMethods = Type("PlayerData").GetMethods().Select(handle => metadata.GetMethodDefinition(handle)).Where(method => metadata.GetString(method.Name) == "ModifyAwardStat").ToArray();
Check(awardMethods.Length == 2 && awardMethods.Select(method => metadata.GetBlobBytes(method.Signature).Last()).OrderBy(value => value).SequenceEqual(new byte[] { 8, 12 }), "completion selects native int overload, not float award accumulation");
var nativeIds = Type("SurgeryType").GetFields().Select(handle => metadata.GetFieldDefinition(handle).GetDefaultValue()).Where(handle => !handle.IsNil)
    .Select(handle => metadata.GetBlobReader(metadata.GetConstant(handle).Value).ReadInt32()).ToArray();
foreach (var entry in SurgeryDefinitions.All) Check(!nativeIds.Contains(entry.Id), "native surgery ID vacancy " + entry.Id);
Console.WriteLine($"Stage 6: {passed} production-logic/metadata checks passed. No Unity/gameplay initialized.");
