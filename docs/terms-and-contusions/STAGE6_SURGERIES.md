# Stage 6: eleven surgeries

Implemented locally using the existing BepInEx `6.0.0-be.791+b782da4`, `net6.0` and generated runtime interop. **Build-only; not deployed or gameplay verified.** All eleven entries now have mechanics. The four original content IDs and the two plugin GUIDs are preserved. Integration/tutorial completion, packaging and deployment remain Stage 7.

## Catalog and tuning

`Surgeries/SurgeryDefinitions.cs` contains the actual identities, default prices and text. `SurgeryMath` is production stat/payment/damage math; `SurgeryBattleState` holds timers and charges; `SurgeriesFeature` connects the catalog to Foundation and native methods. These are deliberately unbalanced technical examples and reuse native enhancement icons.

| Surgery | SurgeryType | Default gold | Mechanic |
|---|---:|---:|---|
| Adrenal Graft | 11020 | **0** | Preserved: stamina recovery rate x1.25 |
| Anchor Heels | 11021 | 35 | Incoming knockback distance x0.25; dodge distance x0.8 |
| Spring-Loaded Knees | 11022 | 40 | Dodge distance x1.3; dodge stamina cost x1.2 |
| Dense Skeleton | 11023 | 55 | Incoming damage x0.85; maximum stamina x0.8 |
| Auxiliary Tendons | 11024 | 40 | Regular melee payment x0.8; maximum HP x0.9; special attacks keep their normal payment |
| Marrow Enrichment | 11025 | 40 | Healing amounts x1.3 before native clamps; maximum HP x0.85 |
| Pain Converter | 11026 | 50 | Actual enemy HP loss / maximum HP x1.5 x maximum stamina, capped at 25% maximum stamina per hit; maximum stamina x0.9 |
| Emergency Clotting | 11027 | 45 | Once per battle, survive an enemy hit losing at least 20% maximum HP: heal 25% of actual loss over four seconds |
| Counterweight Implant | 11028 | 45 | Recover from stun/trip: next successful direct melee contact within five seconds has x1.25 damage and x1.75 knockback |
| Fury Pacemaker | 11029 | 50 | Prevent positive future fury timer/stack gains; convert an eligible grant to 30% maximum stamina at most once per eight seconds |
| Retractable Talons | 11030 | 45 | Successful direct unarmed hits cause five damage over three seconds; one refreshable bleed per attacker/target; armed outgoing damage x0.9 |

The starting 35–55 gold band is intentionally conservative relative to the inspected native paid-tradeoff surgeries, which calculate variable quotes through `SurgeryGenerator.ValueRandomise`, and native stat-equipment comparables (Swift Sandals 40, Quilted Armor 45, Armor Plates 55). These are chosen example prices, **not claimed fixed native surgery quotes or balance recommendations**. Dense Skeleton's broad incoming reduction is the most expensive of these examples. Existing free Adrenal Graft remains free.

In `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`, restart after changing:

- Existing `[AdrenalGraft] EnableAdrenalGraft` and `ForceAdrenalGraftNextOffer`; values migrate/preserve as before. Force defaults false.
- `[Surgeries] EnableAnchorHeels`, `EnableSpringLoadedKnees`, etc., for the ten new entries; defaults true.
- `[SurgeryPrices] AnchorHeels`, `SpringLoadedKnees`, etc.; positive gold costs, accepted range 1–10000. Adrenal Graft has no new price override.

Disabling owned content blocks resuming its custom checkpoint under the existing availability policy. Restore the matching enabled content or roll back the entire paired backup; do not edit saves.

## One offer and completion path

Foundation's public `SurgeryServices.Register(key, price, nativeFactory, forceOffer)` requires an already claimed Surgery identity. There is **no GameLibrary surgery registry**. One shared `SurgeryGenerator.GetSurgeries` postfix samples the combined pool of its native generated options and enabled, unowned, affordable custom providers. Every candidate can participate; there is no old Adrenal one-in-four visit gate and no per-feature last-postfix-wins replacement.

Donation options and the first native targeted cure are protected. Other native options remain candidates as their original objects, with unchanged costs and injury/skill payloads. Sampling preserves an existing returned option count and has no duplicate candidate indices; it does not advance Unity's RNG. If native candidates are exhausted and the returned list is empty, distinct eligible custom enhancements fill up to the requested numberOfOptions. Selection is deterministic from run seed, completed levels and player ID. Returning to the same visit or using the currently deferred surgery reroll therefore does not promise different choices. If every slot is protected, no custom option is inserted. The Adrenal force setting selects it in the first available shared slot for solo testing; it does not bypass ownership or affordability.

`RunDataManager.PerformSurgeryRpc(int,int,int)` is allowed to run normally for eligible custom choices. Before charging, the Execute-stage custom guard rejects already-owned/disabled/unprovided choices, mismatched configured quotes, insufficient money and nested duplicate operations. A refused custom RPC has its execution-stage flag cleared, as the native body would do. Native surgery behavior is otherwise retained: payment, soap healing, injury/skill loops, codex accounting, awards and completion notification.

An operation is confirmed only when that **same player's native `PlayerData.ModifyAwardStat(AwardType.MostSurgeries, 1)` int overload actually returns**, the surrounding RPC returns normally, the money delta matches the quote, and ownership is still absent. The counter is at the native completion tail after payment/effects; observing an Execute wrapper or a free money balance alone cannot grant anything. The confirmed ID is appended once to `PlayerData.previousSurgeries`. Native `PersistentData.Data.GetSurgeryCodex` creates entries for previously unseen IDs; no donor surgery effect is copied. Native checkpoints already serialize the history.

HP-changing acquisition applies the fresh new-max / old-max ratio once to current HP, respecting death and attainable caps. A changed maximum stamina similarly preserves its current percentage once. Getters, refreshes and restores contribute factors without resource grants. Checkpoint capture during an unfinished custom surgery is refused until ownership commits; prior save files are preserved by the shared capture failure policy.

## Native mechanics and interactions

See [native evidence and API boundaries](../foundation/STAGE6_EVIDENCE.md) for the exact hooks. Relevant combinations:

- Anchor x Spring yields dodge distance x1.04. Spring x Runner's Wraps yields dodge cost x0.9. Ordinary running speed and dash duration are not changed by these surgeries.
- Dense x Pain yields maximum stamina x0.72. Native stamina HUD/getters and payment/clamp paths see that maximum. Native ownership and actual resource authority are checked separately.
- Bulk Up x Tendons x Marrow composes maximum HP from a fresh native baseline. With Bulk rank two and native 100 HP: `300 x 0.9 x 0.85`, rounded once to 230. A 180/300 player transitions to 138/230 once. Marrow applies to native potion heal calculation, arena HealRpc, lobby HealRpc, healing surgery damage amounts and Soap for the Surgeon. Full heals remain naturally capped. Integer HP amounts use native-sized rounding; small timed heals may round differently from a single combined heal.
- Dense incoming reduction combines with equipment reduction before native damage; Pain and Clot use the **actual positive enemy HP receipt**, excluding self, internal, friendly, environmental and overkill loss. Existing Locket and Shaky callbacks run before the surgery subscriber; their clamps/cooldowns remain intact. Pain/fury restoration is a one-time stamina grant, not an exertion payment or passive regeneration tick.
- Clot arms before scheduling, heals in four one-second installments totaling its integer budget, and never immediately heals on the triggering hit. Repeated ticks/callbacks cannot rearm it. Death/leaving battle discards the outstanding tail without making the same battle eligible again.
- Counterweight watches actual stun/silent-stun/trip state, then waits for native `IsIncapacitated` (including the untrip recovery grace) to clear. Repeated recovered frames do not grant charges. One direct melee contact reserves the charge; a zero-loss contact releases it, a positive receipt consumes it, and another target cannot claim it while pending. Native successful damage precedes the associated `TryKnockback`; the boost is tied to that attack/target and used once. A late HP receipt keeps the reservation until resolved or battle/reset. Extra damage uses the flat-aware shared composition, preserving Payback Plan's flat-add rule.
- Pacemaker intercepts the closed `NetworkAgentEffect<NetworkAttackEffects.eType>` native timer/stack writes for positive Rage, including both local and RPC-based fury sources. It identifies the player by **their actual AttackEffects instance**. Cooldown reserves before the stamina grant; nested/duplicate callbacks still suppress fury. Negative timer decay/removal and every other effect remain native. Already-active fury can expire normally. The eight-second conversion cooldown persists across battle changes.
- Talons checks the committed weapon's native `WeaponClass.Unarmed`, positive direct melee HP loss and a hostile live target. It uses five one-HP native Slash ticks at 0.6-second intervals; overdue ticks coalesce into one bounded native damage call after a slow frame. Refresh extends expiry to three seconds from the latest hit while retaining cadence; it creates neither another copy nor an immediate tick. Generated directionless damage carries the actual attacker but explicitly cannot enter direct-hit preparation, trigger another Talons bleed, or consume direct-only bonuses. Native damage mitigation/invulnerability still applies. Death/despawn/reset clears affected bleeds; the modifier applies to armed attributed attacks, including normal thrown weapons, without reducing generated Talons ticks.

## Persistence and limits

Native `previousSurgeries` is the sole ownership source. Schema-1 `terms-and-contusions:surgeries/battle-v1` stores Clot's used flag/remaining budget, remaining counterweight and fury times, incapacitation observation and per-target bleed cadence/expiry. It stores no runtime pointer and no second surgery ownership truth. An unsettled Counterweight contact refuses capture; malformed/unknown schemas refuse native restore. Loading assigns timers/history without replaying purchases, HP transitions, stamina or healing.

Native checkpoints do not contain complete arena instances or current stamina/recovery timers. A bleed can resume against a still-live matching spawn token in the same process. A vanished/newly reconstructed enemy is explicitly dropped from the bleed ledger and logged; cross-process recreation of missing arena objects is not claimed. Recovery observation is rebased to native state on restore, preventing a reconstructed unstunned player from receiving a synthetic recovery charge. No full arena snapshot or automatic Steam Cloud sidecar synchronization is implemented.

All split/runtime behavior remains unverified: closed generic HarmonyX/IL2CPP interception, native completion ordering, actual UI price/term binding and card fit, HP/stamina/HUD authority, normal eligibility, physics/knockback, enemy mitigation, timing and checkpoint continuity need manual testing after Stage 7. New terms use English fallback; existing twelve-language Adrenal aliases remain. Surgery reroll animation-with-identical-options remains deferred.

## Targeted manual checks after final deployment

Do not run these automatically. Back up the full user-data directory and matching DLL/configuration pair first.

1. For every row, find its normal surgeon card; check name, full description, icon/category, price, native payment and one history entry. Verify insufficient funds and a repeated/stale selection grant nothing. Adrenal must still cost zero and increase only recovery rate.
2. Measure both benefits and tradeoffs individually; repeat getters/menu/scene refreshes. Buy Tendons/Marrow with partly depleted HP and Dense/Pain with partly depleted stamina. Verify one proportional transition, HUD agreement and no repeated refill or compounding. Combine with Bulk Up, Wraps, Apron and Overheating.
3. Pain: compare actual 5% HP loss with 7.5% maximum-stamina restoration, then test the 25% cap and self/environment/friendly exclusions. Clot: survive a 20% hit; verify no immediate heal, four-second total and no second trigger in that battle. Confirm death stops the tail and a genuine next battle rearms.
4. Counterweight: test stun and trip recovery separately, including the untrip grace; hit/miss/block, five-second expiry, a multi-target swing and repeated UI/tick callbacks. Only one successful contact receives both damage and knockback bonuses.
5. Pacemaker: native fury-producing skills, shared/effect-based sources, full/missing stamina, repeated callbacks, cooldown expiry, battle change and save/resume. Fury must remain prevented throughout cooldown, and existing fury must not be extended.
6. Talons: isolated unarmed hit against an unmitigated enemy should deal five additional HP across three seconds, never immediately. Repeated unarmed hits refresh one bleed's cadence; armed, thrown, blocked, generated and self/environment damage must not create bleeds. Test multiple targets, native mitigation, target/attacker death/despawn, battle cleanup and normal weapon damage penalty.
7. Acquire multiple surgeries together; save/resume matching checkpoints and an older paired backup. Verify independent ownership, remaining timers and no resource replay. Fresh run starts with none. Keep native SaveData and matching sidecar archive together; old v0.7.3 cannot read the new protocol. See [paired restore/rollback](../foundation/PERSISTENCE.md).

Nongame checks: `pwsh -NoProfile -File .\scripts\test-stage6.ps1`. This builds both plugins and the standalone example and exercises actual production logic plus installed metadata without initializing the game. Build success is not runtime success.
