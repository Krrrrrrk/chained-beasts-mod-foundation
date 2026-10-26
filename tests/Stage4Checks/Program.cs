using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TermsAndContusions.Equipment;
using ChainedBeasts.ModFoundation.Logic;

int passed = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
void Near(double actual, double expected, string name) => Check(Math.Abs(actual - expected) < 1e-8, name);
void Reject(Action callback, string name) { try { callback(); } catch (InvalidOperationException) { Check(true, name); return; } throw new Exception("FAIL accepted: " + name); }
EquipmentBattleState RoundTrip(EquipmentBattleState state, double now) => EquipmentBattleState.Restore(JsonSerializer.Deserialize<EquipmentSnapshot>(JsonSerializer.Serialize(state.Capture(now))));

Check(EquipmentDefinitions.All.Length == 10 && EquipmentDefinitions.All.Select(value => value.Id).Distinct().Count() == 10 &&
    EquipmentDefinitions.All.Select(value => value.Key).Distinct().Count() == 10, "ten independent explicit equipment definitions beyond wraps/sling");
foreach (var entry in EquipmentDefinitions.All)
{
    Check(entry.Price >= 40 && entry.Price <= 75 && entry.Id >= 2000 && entry.Id < 3000, "tunable native-range identity/price " + entry.Name);
    Check(EquipmentDefinitions.Find(entry.Id) == entry && EquipmentDefinitions.Get(entry.Kind) == entry && entry.Description.Length > 20, "identity/text lookup " + entry.Name);
}
Near(EquipmentMath.Hood(true, true, true, 29, 100), 1.35, "hood below threshold");
foreach (var hp in new[] { 30d, 100d, 0d }) Near(EquipmentMath.Hood(true, true, true, hp, 100), 1, "hood rejects boundary/dead " + hp);
Near(EquipmentMath.Hood(false, true, true, 29, 100), 1, "unequipped hood neutral");
Near(EquipmentMath.Hood(true, false, true, 29, 100), 1, "hood excludes ranged");
Near(EquipmentMath.Hood(true, true, false, 29, 100), 1, "hood excludes props/friendly targets");
foreach (var (gold, factor) in new[] { (-100, 1d), (0, 1d), (9, 1d), (10, .99d), (199, .81d), (200, .8d), (201, .8d), (int.MaxValue, .8d) })
{
    Near(EquipmentMath.Purse(true, gold), factor, "purse floor/cap for " + gold);
    Near(EquipmentMath.Purse(false, gold), 1, "removed purse neutral for " + gold);
}
Near(EquipmentMath.Purse(true, 100), .9, "purse current gold before spending");
Near(EquipmentMath.Purse(true, 90), .91, "purse immediately changes after spending");
Near(EquipmentMath.Purse(true, 200) * .8, .64, "independent incoming reductions multiply");
Near(EquipmentMath.Spurs(true, true, true), 1.2, "spurs only sole nearby target");
Near(EquipmentMath.Spurs(true, true, false), 1, "spurs excludes second hostile/out-of-range target");
Near(EquipmentMath.Spurs(true, false, true), 1, "spurs melee only");
Near(EquipmentMath.Spurs(false, true, true), 1, "spurs removed neutral");
Near(DamageComposition.MultiplyExceptFlat(125, 25, 1.5), 175, "scabbard multiplies normal damage, not Payback flat addition");
Near(DamageComposition.MultiplyExceptFlat(DamageComposition.MultiplyExceptFlat(125, 25, 1.35), 25, 1.2), 187, "independent hood/spurs contributions retain one flat addition");
Near(DamageComposition.MultiplyExceptFlat(100, 0, 1.4), 140, "chalk empowers resulting direct throw");
Check(EquipmentMath.Full(5, 5, false) && !EquipmentMath.Full(4, 5, false) && !EquipmentMath.Full(5, 5, true) && !EquipmentMath.Full(1, 0, false), "full durability excludes damaged/default/indeterminate objects");
foreach (var (current, maximum, expected) in new[] { (1, 5, 2), (4, 5, 5), (5, 5, 5), (0, 5, 0), (-1, 5, 0) })
    Check(EquipmentMath.Repair(current, maximum) == expected, "repair normal maximum/no resurrection " + current);
Check(EquipmentMath.ExecutionHeal(100, 60, 100) == 3 && EquipmentMath.ExecutionHeal(101, 60, 101) == 4, "execution heal percentage with integer ceiling");
Check(EquipmentMath.ExecutionHeal(500, 98, 100) == 2 && EquipmentMath.ExecutionHeal(100, 100, 100) == 0, "execution heal attainable cap");
Near(EquipmentMath.LocketStamina(24, 100, 100, 1), 50, "locket restores half max stamina below quarter HP");
Near(EquipmentMath.LocketStamina(25, 100, 100, 1), 0, "locket exact quarter excluded");
Near(EquipmentMath.LocketStamina(0, 100, 100, 100), 0, "locket cannot trigger on death");
Near(EquipmentMath.LocketStamina(24, 100, 100, 0), 0, "attempted zero-damage cannot trigger locket");

foreach (var (equipped, gold, cost) in new[] { (true, 5, 5), (true, 4, 0), (true, 0, 0), (false, 100, 0) })
{
    var state = new EquipmentBattleState();
    Check(state.BeginBattle(equipped, gold) == cost, "ledger initial eligibility " + equipped + "/" + gold);
    Check(state.BeginBattle(true, 100) == 0, "ledger duplicate/equipping late cannot retroactively pay");
    if (cost == 5) state.ConfirmPayment(gold, gold - 5);
    Check(state.LedgerPaid == (cost == 5), "ledger requires verified native payment");
    state.EquipmentChanged(); state = RoundTrip(state, 0);
    Check(state.BeginBattle(true, 100) == 0 && state.LedgerPaid == (cost == 5), "ledger re-equip/reload preserves paid or ineligible state without payment");
    Near(EquipmentMath.Ledger(false, state.LedgerPaid), 1, "ledger bonus removed immediately");
    Near(EquipmentMath.Ledger(true, state.LedgerPaid), cost == 5 ? 1.2 : 1, "ledger re-equip only restores actually paid bonus");
}
var refusedPayment = new EquipmentBattleState(); refusedPayment.BeginBattle(true, 10); refusedPayment.ConfirmPayment(10, 10);
Check(!refusedPayment.LedgerPaid, "unchanged native money does not establish payment");
refusedPayment.ConfirmPayment(10, 5); Check(!refusedPayment.LedgerPaid, "duplicate unrelated money delta cannot retroactively grant ledger");
var pendingPayment = new EquipmentBattleState(); pendingPayment.BeginBattle(true, 5);
Reject(() => pendingPayment.Capture(0), "unconfirmed payment blocks checkpoint instead of replaying a charge");
pendingPayment.ConfirmPayment(5, 0); pendingPayment.ConfirmPayment(0, 0);
Check(pendingPayment.LedgerPaid, "duplicate payment callbacks cannot revoke confirmed paid state");
var ineligible = new EquipmentBattleState(); ineligible.BeginBattle(false, 100); ineligible.ConfirmPayment(100, 95);
Check(!ineligible.LedgerPaid, "money spent elsewhere cannot pay an ineligible late-equipped ledger");
var locket = new EquipmentBattleState();
Check(!locket.ClaimLocket(false, 5, 10, 100) && !locket.ClaimLocket(true, 0, 10, 100) && !locket.ClaimLocket(true, 100, 0, 100), "locket requires equipped qualifying survived damage");
Check(locket.ClaimLocket(true, 5, 10, 100), "locket first qualifying event"); locket.EquipmentChanged(); locket = RoundTrip(locket, 0);
Check(!locket.ClaimLocket(true, 5, 10, 100), "locket duplicate/re-equip/checkpoint cannot refill battle allowance");
Check(new EquipmentBattleState().ClaimLocket(true, 5, 10, 100), "new battle has independent locket allowance");

var openings = new EquipmentBattleState();
var blocked = openings.PlanOpening("blocked", "weapon-a", true, true);
Check(blocked.Bonus && !openings.PlanOpening("parallel", "weapon-a", true, true).Bonus, "one pending opener per physical weapon");
Reject(() => openings.Capture(0), "save refuses unresolved opening reservations");
Check(!openings.ResolveOpening(blocked, false), "blocked hit has no cost or spent allowance");
var landed = openings.PlanOpening("landed", "weapon-a", true, true);
Check(landed.Bonus && openings.ResolveOpening(landed, true) && !openings.ResolveOpening(landed, true), "successful opener spends/costs exactly once");
openings.EquipmentChanged(); openings = RoundTrip(openings, 0);
Check(!openings.PlanOpening("repaired", "weapon-a", true, true).Bonus, "repair/drop/pickup/re-equip/reload retain consumed physical token");
Check(openings.PlanOpening("independent", "weapon-b", true, true).Bonus, "second physical weapon with identical native type is independent");
openings.ResolveOpening(openings.PlanOpening("noop", "weapon-a", true, true), false);
var partial = new EquipmentBattleState(); var notFull = partial.PlanOpening("partial", "weapon-c", true, false);
Check(!notFull.Bonus && !partial.ResolveOpening(notFull, true), "first successful damaged-weapon hit does not grant opener");
Check(!partial.PlanOpening("refilled", "weapon-c", true, true).Bonus, "repair to full cannot rearm a used weapon");
var absent = new EquipmentBattleState(); var ordinary = absent.PlanOpening("ordinary", "weapon-d", false, true); absent.ResolveOpening(ordinary, true);
Check(!absent.PlanOpening("equipped-late", "weapon-d", true, true).Bonus, "equipping after weapon's first success cannot manufacture a new opener");

var pockets = new EquipmentBattleState();
Check(!pockets.ClaimBackstab(true, false, "enemy-0") && !pockets.ClaimBackstab(false, true, "enemy-0"), "mitts require equipped confirmed backstab");
for (var i = 0; i < 10; i++) Check(pockets.ClaimBackstab(true, true, "enemy-" + i), "one gold per distinct enemy " + i);
Check(!pockets.ClaimBackstab(true, true, "enemy-0") && !pockets.ClaimBackstab(true, true, "enemy-10"), "same enemy and eleventh enemy cannot grant more gold");
pockets.EquipmentChanged(); pockets = RoundTrip(pockets, 0);
Check(!pockets.ClaimBackstab(true, true, "enemy-11") && pockets.Capture(0).PocketGold == 10, "mitts cap survives re-equip/checkpoint");
Check(new EquipmentBattleState().ClaimBackstab(true, true, "enemy-0"), "fresh battle independently rewards enemy");
var apron = new EquipmentBattleState();
Check(!apron.ClaimExecution(true, false, "enemy") && !apron.ClaimExecution(false, true, "enemy"), "apron excludes attempted executions/unequipped state");
Check(apron.ClaimExecution(true, true, "enemy"), "confirmed execution claimed"); apron = RoundTrip(apron, 0);
Check(!apron.ClaimExecution(true, true, "enemy"), "duplicate animation/receipt and reload cannot heal twice");

var salvage = new EquipmentBattleState();
Check(!salvage.ClaimRepair(true, false, "weapon", 3, 5) && !salvage.ClaimRepair(false, true, "weapon", 3, 5), "salvage requires recovery of own throw while equipped");
Check(!salvage.ClaimRepair(true, true, "weapon", 5, 5) && !salvage.ClaimRepair(true, true, "weapon", 0, 5), "salvage does not spend allowance at full/broken durability");
Check(salvage.ClaimRepair(true, true, "weapon", 3, 5), "own weapon recovery repairs once"); salvage.EquipmentChanged(); salvage = RoundTrip(salvage, 0);
Check(!salvage.ClaimRepair(true, true, "weapon", 1, 5), "rethrow/drop/re-equip/reload cannot reset repair allowance");
Check(salvage.ClaimRepair(true, true, "different-weapon", 3, 5), "independent weapon repairs separately");
Check(RepresentationTransfer.Matches(true, 1, 1, 2, 2, 0, 0) && !RepresentationTransfer.Matches(false, 1, 1, 2, 2, 0, 0), "same origin zero only transfers within explicit native physical scope");
Check(!RepresentationTransfer.Matches(true, 1, 2, 2, 2, 4, 4) && !RepresentationTransfer.Matches(true, 1, 1, 2, 3, 4, 4) && !RepresentationTransfer.Matches(true, 1, 1, 2, 2, 4, 5), "unrelated native representation cannot inherit allowance token");
var spawns = new SpawnTokenMap<long>(); var enemyToken = spawns.Get(1, 100);
Check(spawns.Get(1, 100) == enemyToken, "duplicate spawn notifications keep same enemy reward identity");
Check(spawns.Get(2, 100) != enemyToken, "distinct runtime objects cannot share reward token through same native generation");
Check(spawns.Get(1, 101) != enemyToken, "pooled object with a new spawn generation has a new reward identity");
var beforeDespawn = spawns.Get(1, 101); spawns.Despawn(1);
Check(spawns.Get(1, 101) != beforeDespawn, "despawn/respawn with a recycled network ID still has a new token");
var beforeReset = spawns.Get(1, 101); spawns.Clear();
Check(spawns.Get(1, 101) != beforeReset, "fresh run cannot inherit old enemy rewards");

var chalk = new EquipmentBattleState(); chalk.Aim(0, true);
Check(!chalk.ReleaseThrow(1.499, true, true), "chalk before stationary threshold neutral"); chalk.Aim(2, true);
Check(chalk.ReleaseThrow(3.5, true, true) && !chalk.ReleaseThrow(3.5, true, true), "chalk fixed threshold and one release consume");
chalk.Aim(4, true); chalk.Aim(5.4, false); chalk.Aim(5.5, true);
Check(!chalk.ReleaseThrow(5.6, true, true), "movement cancels accumulated aim time");
chalk.Aim(6, true); chalk.EquipmentChanged(); chalk.Aim(10, true);
Check(!chalk.ReleaseThrow(10, true, true), "unequip/re-equip cannot retain aim charge");
chalk.Aim(11, true); chalk = RoundTrip(chalk, 12); chalk.Aim(1000, true);
Check(!chalk.ReleaseThrow(1000.49, true, true), "checkpoint restores elapsed aim, not wall-clock offline time");
chalk.Aim(1001, true); Check(!chalk.ReleaseThrow(1003, false, true), "removed chalk cannot empower a throw");
chalk.Aim(1004, true); Check(!chalk.ReleaseThrow(1006, true, false), "release while moving/not aiming cancels preparation");
chalk.Aim(2000, true); chalk = RoundTrip(chalk, 2001); chalk.Aim(3000, true);
Check(chalk.ReleaseThrow(3000.5, true, true), "matching checkpoint resumes remaining eligible aim duration exactly");
Reject(() => EquipmentBattleState.Restore(new EquipmentSnapshot { SchemaVersion = 999 }), "unknown equipment schema rejected before restore");
Reject(() => EquipmentBattleState.Restore(new EquipmentSnapshot { LedgerPaid = true }), "unpaid ledger state rejected");
Reject(() => EquipmentBattleState.Restore(new EquipmentSnapshot { ChalkElapsed = double.NaN }), "nonfinite aim state rejected");
Reject(() => EquipmentBattleState.Restore(new EquipmentSnapshot { PocketGold = 11 }), "over-cap state rejected");
Reject(() => EquipmentBattleState.Restore(new EquipmentSnapshot { OpenedWeapons = new[] { "same", "same" } }), "duplicate identity state rejected");
Reject(() => EquipmentBattleState.Restore(new EquipmentSnapshot { PocketGold = 1 }), "gold and per-enemy receipt ledger must match");

var root = Path.GetFullPath(args[0]);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/terms-and-contusions/manifest.json")));
var entries = manifest.RootElement.GetProperty("entries").EnumerateArray().ToArray();
var stage = manifest.RootElement.GetProperty("stage").GetInt32();
Check(stage >= 4 && entries.Count(value => value.GetProperty("implementationStatus").GetString() == "planned") == (stage >= 6 ? 0 : stage >= 5 ? 10 : 20),
    "catalog respects completed stage boundaries and planned future content");
foreach (var entry in EquipmentDefinitions.All)
{
    var recorded = entries.Single(value => value.GetProperty("key").GetString() == entry.Key);
    Check(recorded.GetProperty("numericId").GetInt32() == entry.Id && recorded.GetProperty("price").GetInt32() == entry.Price &&
        recorded.GetProperty("implementationStatus").GetString() == (stage >= 7 ? "implemented-runtime-unverified" : "implemented-build-only"), "manifest matches actual equipment " + entry.Name);
}
using var gamePe = new PEReader(File.OpenRead(Path.Combine(args[1], "BepInEx/interop/Assembly-CSharp.dll")));
var metadata = gamePe.GetMetadataReader();
TypeDefinition Type(string name) => metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle)).Single(type => metadata.GetString(type.Name) == name);
foreach (var expected in new[] {
    ("Player", "CompleteExecution", 3), ("Player", "ModifyAttackDamage", 7), ("Player", "EquipWeapon", 3), ("Player", "ReceivePickup", 8),
    ("Player", "DropPickupRpc", 3), ("Player", "HasEquipment", 1), ("Player", "RefreshWeaponDurabilityDisplay", 1),
    ("Pickup", "RequestPickupRpc", 1), ("PickupManager", "SpawnPickup", 12), ("Projectile", "ToPickup", 0),
    ("Projectile", "FixedUpdate", 0), ("Projectile", "LandedOnGround", 2), ("Projectile", "HandleTriggerWithOtherHealthObject", 3),
    ("Projectile", "HandleTriggerWithAgent", 1), ("Projectile", "EmbedInTarget", 3),
    ("NetworkHealthObject", "HealRpc", 1), ("RunDataManager", "ModifyMoney", 1), ("Enemy", "OnNetworkSpawn", 0), ("NetworkAgent", "OnNetworkDespawn", 0)
})
{
    var methods = Type(expected.Item1).GetMethods().Select(handle => metadata.GetMethodDefinition(handle));
    Check(methods.Count(method => metadata.GetString(method.Name) == expected.Item2 && method.GetParameters().Count(handle => metadata.GetParameter(handle).SequenceNumber > 0) == expected.Item3) == 1,
        $"installed unique Stage 4 target {expected.Item1}.{expected.Item2}/{expected.Item3}");
}
var nativeIds = Type("PurchaseType").GetFields().Select(handle => metadata.GetFieldDefinition(handle).GetDefaultValue()).Where(handle => !handle.IsNil)
    .Select(handle => metadata.GetBlobReader(metadata.GetConstant(handle).Value).ReadInt32()).ToArray();
foreach (var entry in EquipmentDefinitions.All) Check(!nativeIds.Contains(entry.Id), "installed native ID vacancy " + entry.Id);
Console.WriteLine($"Stage 4: {passed} production-logic/metadata checks passed. No Unity/gameplay initialized.");
