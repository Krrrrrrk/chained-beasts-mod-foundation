# Stage 4: the twelve equipment entries

Implemented locally against BepInEx `6.0.0-be.791+b782da4` and the existing `net6.0` runtime interop. **Build-only, not deployed; all new native behavior awaits manual gameplay testing.** Stage 4 adds ten equipment mechanics and preserves Runner's Wraps and Counterweight Sling. Affliction and surgery expansion remains in Stages 5 and 6.

## Catalog and prices

`Equipment/EquipmentDefinitions.cs` is the explicit ID/key/price/text table. Prices are experimental constants, not balanced recommendations. Inspected native comparisons are Swift Sandals 40, Quilted Armor 45, Armor Plates 55, Leather Strop 60, Ankh 65 and Leather Gloves 75. These support the initial 40–75 gold band. Each new entry has its own equipment identity/category/price and reuses only native sprite/model references; no donor item's mechanics are copied.

| Equipment | Purchase ID | Price | Implemented mechanic |
|---|---:|---:|---|
| Executioner's Hood | 2488 | 55 | ×1.35 direct melee damage to hostile enemies below 30% effective HP **before** the hit; exactly 30% does not qualify |
| Lead-Lined Purse | 2489 | 50 | Incoming multiplier `1 - min(.20, .01 * floor(max(gold,0)/10))`, recomputed from current gold |
| Borrower's Scabbard | 2490 | 60 | A nondefault weapon at full normal durability receives ×1.5 damage on its first successful direct hit per battle, with one extra durability cost |
| Butcher's Apron | 2491 | 55 | A confirmed execution kill heals 3% maximum HP, rounded up to the next integer HP and capped by attainable health |
| Last-Gasp Locket | 2492 | 60 | Once per battle, actual hostile-enemy HP loss leaving the player alive below 25% HP restores 50% max stamina; exactly 25% and death do not qualify |
| Mercenary's Ledger | 2493 | 50 | If equipped at the real battle start and at least 5 gold is available, pay 5 once; verified payment enables ×1.2 attack damage that battle while equipped |
| Pickpocket's Mitts | 2494 | 45 | First successful direct backstab per hostile enemy grants 1 gold; at most 10 per battle |
| Chalk Bag | 2495 | 45 | Aim a normal held throw while stationary for 1.5 scaled gameplay seconds; release consumes preparation and that throw deals ×1.4 direct damage while the bag remains equipped |
| Salvager's Hook | 2496 | 65 | Successful recovery of the player's own thrown weapon repairs one raw durability, once per physical weapon per battle, capped by its normal maximum |
| Duelist's Spurs | 2497 | 50 | ×1.2 direct melee damage if the target is the only living active hostile within four metres of the player; the target must itself be in that radius |
| Counterweight Sling | 2498 | 45 | Preserved Stage 3: ×2 normal throw range/charge speed, multiplying Longshot's contribution |
| Runner's Wraps | 2499 | 40 | Preserved identity and behavior: dodge stamina cost ×0.75 while equipped |

Native equipment has **one active equipment slot**, not twelve simultaneously active slots. The twelve entries join the normal shop pool; normal generation, prices, currency callbacks, equipment replacement and history remain native. No new forced offer is enabled by default. Existing Runner's Wraps test overrides are preserved.

## Event and modifier integration

`EquipmentFeature.cs` connects the ten new entries to Foundation. `EquipmentMath.cs` supplies production calculations; `EquipmentBattleState.cs` supplies the production paid/spent/reward/reservation/timing ledger. Existing wraps and sling retain their own feature classes.

- Hood/Spurs snapshot the live target/hostile roster in `PreparingDirectHit`. Purse uses `IncomingDamage`. Ledger uses the existing otherwise-normal `AttackDamage` modifier channel. Independent percentage sources multiply. `DirectHit.MultiplyDamage` excludes `FlatDamageBonus`, so equipment does not multiply Payback's stored flat addition.
- Backstab status comes from the native `Player.ModifyAttackDamage(..., isBackstab, ...)` decision inside the same scoped attempt. The reward requires a later positive HP receipt, not the calculation call alone. `DirectHit.TargetId` is captured before damage/despawn, so a killing hit retains the same enemy reward identity.
- Apron observes `Player.CompleteExecution`'s actual execution-damage dispatch, then requires positive HP loss and a dead target in the Execute stage of `TakeDamageRpc`. `ConfirmExecutionRPC` starts the animation and is deliberately **not** proof of completion. Healing uses native `HealRpc`; stamina uses native `RestoreStamina` and its normal clamp. Internal/self/environment/friendly damage cannot trigger the enemy-damage event.
- Locket, Ledger, Mitts, Scabbard and Hook keep allowances independently of equipment changes. Equipping Ledger late does not pay or grant a bonus. Re-equipping an already paid Ledger restores its bonus without another payment. An unchanged or unrelated money delta does not confirm payment.
- Scabbard reserves one candidate per physical weapon. A blocked/zero-HP candidate releases its reservation; a successful opener spends it once. A weapon's first successful direct contact is remembered even if damaged or the Scabbard was absent, so repairing or equipping later cannot manufacture a new opener. Multi-target attacks can obtain at most one opening bonus for that weapon. Ordinary generated/area effects are outside the direct-hit route.
- The extra durability cost is queued until normal contact processing has finished, and flushed before a checkpoint. Held weapons use native `LoseDurability` with the standard UI/break handling. A narrowly scoped check prevents Leather Gloves' random exemption from cancelling this specific extra cost if equipment changes before it is paid; normal durability loss retains native behavior. In-flight/world representations update the corresponding native durability; a zero-durability world pickup is destroyed. A vanished/broken weapon cannot be driven below zero.
- Chalk uses native `IsAimingThrow`, held weapon and horizontal velocity. Movement, losing aim, leaving Solo, or changing equipment cancels preparation. Release consumes it even if the throw misses; one attempt tag follows the actual launched projectile. Equipping the bag only after release creates no bonus. Offhand abilities, bows and thrown enemies are not the normal held-throw path. Main-menu/lobby frames without actual player objects do not query the native error-logging `PlayerObject` getter.

## Physical identity and lifecycle

`Runtime/NativeWeaponIdentity.cs` assigns independent GUID tokens to physical weapons. Native `PickupData` pointers are only transient aliases, retained with their wrappers; no pointer, hash of a pointer, donor ID or registration order is a serialized identity. Native `originNetworkObjectID` is **not unique** and is never used alone as the identity.

An explicit source scope transfers a token through normal throw launch/generation, projectile landing/embedding, `PickupManager.SpawnPickup`, `Pickup.RequestPickupRpc`, `Player.ReceivePickup` and successful `EquipWeapon`. Automatic dropping of an old weapon during pickup has its own outgoing source. Matching type/model/origin merely validates that transfer; identical weapons outside a transfer get independent tokens. A converted native object with a different type/model is treated as a different physical object.

Salvage requires an actual successful own-throw recovery, not a drop or an attempted pickup. Recovery while unequipped grants no repair. A full or already broken weapon is not repaired and spends no repair allowance. A later genuine own-throw recovery may use an allowance that has never produced a repair. Repair does not reset Scabbard.

Enemy reward GUIDs similarly follow a native spawn generation. Duplicate spawn notifications keep the same identity; despawn/respawn creates a new one even if Netcode recycles an ID. The hit captures that identity before death. Scene/UI refreshes do not reset ledgers. Foundation's existing run/completed-level battle key deduplicates battle starts; a new battle resets allowances, and a fresh/abandoned run clears state.

## Checkpoints and rollback

Native equipment/history remains authoritative. Feature state is schema 1 under `terms-and-contusions:equipment/battle-v1`, with paid/evaluated Ledger flags, spent Locket, per-enemy gold receipts/count, consumed physical opener/repair tokens, execution receipts and bounded elapsed aim time. Checkpoint capture clones these records; validation rejects unknown schemas, malformed/duplicate tokens, impossible payment states or mismatched reward counts before native restore.

Restoration assigns state without healing, paying, granting gold, repairing or replaying an acquisition. Outstanding opener/payment confirmations block capture and preserve the previous save; extra durability is flushed first. Aim restores elapsed eligible time, not offline wall-clock time. Equipment changes do not clear consumed allowances.

The inspected native player/run checkpoint does **not** serialize arena weapon/pickup/projectile/enemy instances. This feature does not turn it into a complete arena snapshot. Existing live objects retain their runtime alias tokens during a same-process restore; objects newly created by native scene/resume reconstruction are new physical spawns. Saved battle-level caps and paid/spent flags remain preserved. Cross-process restoration of a vanished arena object is not claimed. Verify normal native checkpoint/scene behavior manually, including same/older checkpoints.

Pre-change checkpoint: `backups/expansion-stage4-20261025-182500` contains prior source/docs/tests/scripts/examples/build artifacts, installed v0.7.3, loader configuration, user data and hashes. No installation rollback is needed now because nothing was deployed. After eventual deployment, back up/restore native SaveData **and matching sidecars** with the corresponding compatible DLLs/configuration. Downgrading only the DLL against a save owning new equipment is unsupported and can block restoration. See [paired-save policy](../foundation/PERSISTENCE.md) and [migration](../foundation/CONFIGURATION_MIGRATION.md).

## Configuration and checks

After final deployment, `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg` adds these startup booleans under `[Equipment]`, default true:

`EnableExecutionersHood`, `EnableLeadLinedPurse`, `EnableBorrowersScabbard`, `EnableButchersApron`, `EnableLastGaspLocket`, `EnableMercenarysLedger`, `EnablePickpocketsMitts`, `EnableChalkBag`, `EnableSalvagersHook`, `EnableDuelistsSpurs`.

Existing `[RunnersWraps]` and `[CounterweightSling]` switches/IDs/prices remain unchanged. Restart to apply changes. Removing/disabling an owned content identity blocks incompatible checkpoint restoration; it does not delete saved ownership. New equipment text uses Foundation's explicit **English fallback**. Native sprites/models are reused; original icon import, balance and card layout are not claimed verified.

Run `pwsh -NoProfile -File .\scripts\test-stage4.ps1`. It builds both production plugins and the independent example, runs all earlier checks, and links the actual equipment math/state and shared composition/transfer/generation logic into a nongame runner. It also checks installed hook signatures/ID vacancies and manifest prices/identities. No game or live gameplay handler is initialized.

Final result: all three affected projects build with zero warnings/errors; **191 Stage 4 checks plus 682 earlier checks pass (873 total)**, alongside the intermediate-deployment refusal check. Installed plugin/configuration/user-data files, game build, complete specification and prior screenshots remain unchanged. See [progress and artifact hashes](../EXPANSION_PROGRESS.md).

## Manual test after final authorized deployment

1. Find/buy each of the twelve equipment choices through the normal shop. Check its own name, description, price, icon/model reuse, slot replacement and native history. Disable a switch on a backed-up disposable run and verify the stated ownership/restore policy.
2. Hood: compare melee at 29%/30% target health, ranged hits and nonhostile objects. Purse: compare 9/10/199/200+ gold and spending, including another native incoming reduction. Spurs: zero/one/two living hostiles at the four-metre boundary; target outside the radius must not qualify.
3. Scabbard: hit with a full weapon, check damage plus one extra durability, miss/block first, multi-target contacts, real break/Waste Not, drop/recover, repair to full, replace/re-equip, a second identical weapon, and a new battle. No duplicate opener or cost; ordinary weapon costs remain native.
4. Apron: finish a real execution, verify capped 3% heal, fail/cancel one and observe no heal. Locket: enemy damage below/exactly 25%, zero/blocked/self/environment/friendly damage and death; only one qualifying restoration per battle despite equipment changes.
5. Ledger: start equipped with 4/5+ gold, verify one 5-gold payment and ×1.2 damage; equip late; remove/re-equip after payment; duplicate state transitions; restore and enter the next battle. No retroactive/free/repeated payment.
6. Mitts: successful backstab twice against one enemy, another enemy, blocked attempts, a killing backstab/despawn, ten then eleven distinct enemies, re-equipping and a new battle. Verify one gold per eligible enemy and ten-gold cap.
7. Chalk: aim 1.49/1.5+ seconds, move/cancel/throw/miss, remove the bag before impact, equip only after release, repeat throws and change scenes. Compare equal-charge damage separately from Longshot/sling range. Hook: throw and recover a damaged weapon repeatedly, drop without throwing, recover unrelated/identical/embedded weapons, full/broken durability and a new battle. Check HUD and native maximum.
8. Save/restore same and older paired checkpoints with paid/spent/reward state. Confirm no payment/heal/repair/gold replay or allowance reset, including a fresh run with the same seed. Check logs for native hook, unresolved receipt, durability and capture/restore errors. Native reconstructed arena objects are new spawns, not restored physical instances.

All native hooks, actual resource changes, authority/ordering, multi-target behavior, UI layout, physical transfer and save/resume behavior remain **awaiting manual gameplay testing**. Surgery completion/offers/rerolls remain unchanged. **Next: Stage 5's afflictions. Stage 4 stops here.**
