# Expansion progress

Updated: 2026-10-26 (America/New_York). Complete **one implementation stage per turn**, then stop. The [complete original specification](EXPANSION_SPEC.md), including its staged execution override, remains authoritative and byte-identical (SHA-256 `22943c84747c64dd78b502556ba7c9129c1f47fc8d79a3431be2ebcf0b0165de`).

## Reconciled baseline and current status

- Initial published baseline: `ca23dcb1f33444dc94e0ca6d40e417a9b2939e07`, manual-tested `v0.7.3`. Stage 1's uncommitted split, guides, screenshots and build/check workflow were preserved before extending them.
- **Stages 1–7 are complete as implementations; 0.8.3 is the current local installation/source update.** The author reported no crashes in a subsequent 0.8.2 gameplay test. This does not verify every catalog mechanic/checkpoint. [The community update](COMMUNITY_UPDATE_0.8.3.md) adds TC description tags, normal offer defaults and a first-person README; those new changes await manual checks. Earlier crash reports remain historical evidence. Stage 6 preserves the complete uncommitted Stages 1–5 source/build baseline and extends only surgeries plus their required shared completion/stat/damage support.
- Installed plugins are Foundation and TermsAndContusions `0.8.3`. The community update supersedes the historical [0.8.0 Stage 7 handoff](STAGE7_HANDOFF.md) for current deployment status. The old pre-expansion v0.7.3 paired checkpoint remains preserved; hotfix checkpoints instead contain the current run/configuration and failing 0.8.0 DLLs.
- No game launch, screenshots, live grants/purchases/gameplay calls or game save edits were performed by the agent. The author explicitly authorized this source update to be committed/pushed to GitHub; binary Release/Thunderstore publication is separate. The local mod config intentionally clears the four Force flags and sets the new master false. Stages 1–6 were not deployed; Stage 7 performed the authorized backed-up local two-DLL deployment. Build/check commands still refuse implicit deployment.
- Historical initial Stage 7 integrity verification: **25 configuration/user-data files** remained byte-identical; only the old plugin was replaced by two verified production DLLs (installed counts 2/2/23). Four original game binaries, all five screenshots and the exact specification remain unchanged. GameAssembly SHA-256 remains `72f2892607b0772470caae5db766dc07aaf636c3edfe26cefe019a5b9ddae56b`.

## Community update — 0.8.3

- [x] Preserve source/DLL/configuration/full current user data before changes.
- [x] Mark all pack-owned descriptions TC exactly once, including legacy translations; leave vanilla text/names/IDs unchanged.
- [x] Gate Force options behind a false-by-default developer master; clear all four local Force flags. Ordinary generated pools and existing save ownership remain intact.
- [x] Rewrite the README in first person and record the author's crash-free 0.8.2 test without claiming exhaustive runtime verification.
- [x] Build four assemblies cleanly; pass 1,774 nongame checks plus deployment guard; package/deploy 0.8.3 with the game closed.
- [ ] Fresh manual check of TC cards/banners/details and normal offer generation; no automatic gameplay test.
- [x] Source publication to main is explicitly authorized for this handoff; compiled packages/backups/generated assemblies/raw logs stay excluded from Git.

## Second crash/presentation hotfix — 0.8.2 (historical)

- [x] Preserve 0.8.1 logs/source/DLLs/configuration/current full user data. The prior trampoline flood and missing-sprite failures are absent; another native gameplay crash remains observed.
- [x] Identify and skip two shared charged-weapon Attack wrappers; retain their unique base Attack observation and add a shared-entry patch guard. No native fault stack establishes the sole crash cause.
- [x] Patch the actual custom injury reveal naming helper with registered name/severity terms, including Lead Belly.
- [x] Shorten per-rank/severity cards to at most 125 characters; trainer shows next rank, owned views show actual rank. Preserve full prior mechanics text in manifest references.
- [x] Build four assemblies cleanly; 1,729 nongame checks plus explicit deployment guard pass.
- [x] Package/deploy both 0.8.2 DLLs with the game closed; reconcile installed/build/archive hashes and manifest. Current 3 configuration/16 user-data files and native/specification hashes are unchanged. Exact recovery points are in the second report.
- [x] Subsequent manual 0.8.2 session reported no crashes. This report alone does not pass every name/layout, individual mechanic or save/resume check.

## First post-deployment crash hotfix — 0.8.1 (historical)

- [x] Preserve failing logs, pre-fix source/DLLs and current user data.
- [x] Inspect native bodies/addresses; remove the folded Projectile.OnDespawn detour and guard that target.
- [x] Fix preferred-native-sprite fallback for all eleven failed skill definitions.
- [x] Build four assemblies without warnings/errors; pass 1,674 nongame checks plus deployment guard.
- [x] Package and deploy 0.8.1 locally through the closed-game checkpoint workflow.
- [x] Verify installed/build/package DLL hashes and preserve all 3 current configuration plus 14 current user-data files byte-for-byte; native build and original specification hashes unchanged.
- [x] User retested 0.8.1: original startup failures were gone, but gameplay crashed again after acquiring Pick Your Moment. This is a failed runtime test; the second hotfix report supersedes the old retest-pending status. No further expansion stage or publication is implied.

## Stage checklist

| Stage | Status | Implemented work / boundary |
|---|---|---|
| 1 — Separate the plugins | **Complete; runtime unverified** | Distinct Foundation/pack assemblies and GUIDs; hard version-range dependency; four existing features moved once; config migration; build/check scripts and deployment block. |
| 2 — Persistence and shared infrastructure | **Complete; runtime unverified** | Versioned exact-checkpoint state; native async success observation and atomic snapshots; old-marker import; stable identities/collisions; canonical ownership/removal/restoration; shared modifiers/events/lifecycle; real Solo guard; standalone skill and nongame checks. |
| 3 — Skills and throwing | **Complete; runtime unverified** | All 12 skills at four ranks; proportional Bulk Up; Longshot + Counterweight Sling; direct-combat events/state, actual throw/preview/charge adapters and production calculation matrix. |
| 4 — Remaining equipment | **Complete; runtime unverified** | All 12 equipment entries; actual execution/backstab/recovery evidence, physical representation identity, paid/spent/per-enemy/per-weapon ledgers, guarded aim tick, extra durability, removal/re-equipping and checkpoint state. Wraps/sling preserved. |
| 5 — Potion afflictions | **Complete; runtime unverified** | All 11 afflictions; native effective severity and normal potion healing/acquisition/cure; pending-state cleanup, actual HP/stamina/shield/dodge/crowd events, deterministic per-action focus and validated remaining-time state. |
| 6 — Surgeries | **Complete; runtime unverified** | All 11 mechanics; shared native/custom offers, genuine native completion receipts, once-per-run history, prices, proportional resource transitions, Clot/Counterweight/Fury timers and refresh-only Talons bleed. |
| 7 — Integration and delivery | **Complete; manual runtime testing pending** | All 46 rows reconciled; compiled category tutorials, community guides, full checklist, local ZIPs, tested guarded deployment/paired rollback and verified two-DLL installation. No publication. |

## Stage 2 implementation and evidence

- `Content/ContentRegistry.cs`: stable owner/key/category/ID claims, native enum checks, duplicate/legacy-slot collision rejection, disabled reservations. Four pack keys and IDs remain unchanged.
- `State/{CheckpointModels,CheckpointStore,RunStateSession}.cs`: schema 1, independent multiple ranks/severities/order/feature JSON, exact run seed/start time/player roster/generation/native hash, immutable snapshots, older-checkpoint restoration, migration-once, dormant preservation/availability policy, new/abandoned-run cleanup.
- `Runtime/NativePersistence.cs`: native run capture; serialized SaveData payload matching; Steam/Standalone WriteFile returned-task success; prepare failure blocks that native write while preserving prior SaveData; exact-disk proof for crash-window pending recovery; early restore refusal; dictionary assignment without grants; native equipment/surgery histories authoritative.
- One shared negative discriminator in the reserved native None slot identifies the checkpoint generation. It stores no content rank. New content uses independent sidecar records; old Bulk Up/Leadfoot marker meanings are import-only. Missing/mismatched state, unknown schema/protocol or unavailable owned content blocks run restoration with explicit logs. See [protocol, backup and rollback](foundation/PERSISTENCE.md).
- `Runtime/{FoundationServices,NativeOwnership,NativeCatalogAndStats}.cs`, `Logic/`, `Stats/`: canonical Add/RemoveSkill/Injury events, signed-count limits, real transition/reentrancy handling, fresh-baseline stat contributions, equipment changes, reset/restore and minimal battle scope.
- Bulk Up drops the processed-level cache and private save bridge. Actual gains/losses scale current HP by new max / old max with attainable/death clamps; restore never replays it. The F1 menu does not repeat that custom transition. Its confirmed v0.7.3 forced-offer **backing-row** banner fix remains intact; no new banner detours/scanners were introduced.
- Leadfoot uses shared persistence/modifiers and native effective HasInjury. Runner's Wraps and Adrenal Graft use shared stat channels with native ownership predicates. Their original values are retained; this stage does not implement future catalog mechanics.
- `Runtime/FoundationText.cs`: reusable owned term/translation API, explicit English fallback and pooled-label integration; the pack retains its separate twelve-language table and historical aliases. Failed identities stop active lookup aliases/modifiers.
- Developer tools and all forced-offer overrides require real Solo mode, one local client, one player and host authority. Temporary cheats clear on reset/leaving that context. Host authority alone is no longer sufficient.
- `examples/StandaloneContentExample/`: buildable independent Training Footwork skill, ID `12000`, GUID `example.chainedbeasts.standalonecontent`, Foundation dependency, real definition/text/modifier integration and shared independent rank/order persistence. Outside the 46-entry pack and all normal deployment.
- [Native-code evidence](foundation/STAGE2_EVIDENCE.md) identifies actual inspected methods and the asynchronous success boundary. Cpp2IL declaration stubs are not runtime references or proof of native bodies.

## Content accounting

The [46-entry manifest](terms-and-contusions/manifest.json) remains **12 skills, 12 equipment, 11 afflictions and 11 surgeries**.

- **46 `implemented-runtime-unverified` entries** account for all categories. Original four identities/behaviors remain represented, with original v0.7.3 manual evidence historical only; the split has no runtime verification. Adrenal Graft retains ID `11020`, free price/effect and confirmed completion.
- **Zero entries remain planned.** Native enum metadata verifies skills `11000`–`11011`, equipment `2488`–`2497`, sling `2498`, new afflictions `11011`–`11020` and surgeries `11020`–`11030`. Matching numbers across different categories do not collide. No placeholder is counted as implemented.
- Bulk Up now has four ranks / cumulative **2/3/4/5x max HP**, proportional actual rank transitions and no healing replay on restore. Old imported rank one stays rank one/2x.
- Longshot + Counterweight Sling were implemented in Stage 3; Retractable Talons including its required bleed is implemented in Stage 6. Recall Ring, Debt Collector, Bowling for Beasts and Blood Bank Flask remain excluded.
- The standalone skill is a teaching consumer, not a 47th production-pack entry.

## Stage 3 implementation and prerequisite safeguards

- `src/TermsAndContusions/Skills/{SkillDefinitions,SkillMath,SkillsFeature}.cs`: explicit identities, twelve native four-rank definitions, current/next/max-rank text, targeted trainer/character/tooltip views and actual formulas. Measured Violence/Running on Fumes use pre-payment stamina; Catch Your Breath changes stationary delay only; Expensive Taste reads live gold; Fresh Meat checks pre-hit full enemy HP; Pick Your Moment consumes readiness on committed attempts, including misses.
- `Skills/SkillCombatState.cs`: Mixed Martial Arts' single opposite-category six-second prime; Payback Plan's actual enemy-caused HP-loss reservoir, fixed 25%-max-HP cap and five-second expiry. One contact reserves/spends each bonus only on confirmed HP loss; blocked hits release it. Validated feature schema stores remaining times without replaying resources. Waste Not observes genuine durability breaks; Breathing Room counts living nearby hostiles.
- Foundation `Runtime/{CombatServices,NativeCombat}.cs` and `Logic/CombatTransitions.cs`: committed attempts, pre-cost snapshots, scoped direct contacts, native Execute-stage actual HP receipts and durability breaks. Nested overrides share identity; generated/area damage cannot spend direct bonuses. Foundation alone creates no pending direct contacts without subscribers.
- Foundation `Runtime/NativeThrowing.cs` / `Logic/ThrowPhysics.cs`: normal throw RPC/coroutine/launch provenance, pooled launch generation, horizontal-only ballistic transform, matching arc/landing marker/aimed targeting and one native charge clock. No broad projectile or global Time multiplier.
- `Equipment/CounterweightSlingFeature.cs`: native shop/equipment history, ID 2498, experimental price 45 and native visual reuse. Longshot ranks 0–4 contribute 1/2/3/4/5x; sling independently doubles these to 2/4/6/8/10x. Charge time is inverse; phase transitions retain ordinary frame quantization. Changing sources during charge affects future clock steps only.
- `FoundationServices` / `NativePersistence`: feature-state validation before native restore, and failed feature capture blocks its corresponding native write. Unsettled HP receipts/charge reservations also block checkpoint capture rather than restoring a spendable bonus twice. A timeout logs and retains the reservation, without inventing success; late confirmation can resolve it. This conservative saving refusal remains a manual verification point.
- Existing startup switches remain; eleven `Skills.Enable<Name>` switches and `CounterweightSling.EnableCounterweightSling` default true. No new forced offers or cheats default on. See [formulas, configuration, supported paths and manual matrix](terms-and-contusions/STAGE3_SKILLS.md) and [inspected native evidence](foundation/STAGE3_EVIDENCE.md).

## Stage 4 implementation and prerequisite safeguards

- `Equipment/{EquipmentDefinitions,EquipmentMath,EquipmentBattleState,EquipmentFeature}.cs`: ten new independent equipment definitions, normal shop/price/text registration and actual mechanics. Prices 45–65 are documented against native 40–75 equipment comparisons; English fallback and native sprite/model reuse are explicit. Runner's Wraps and sling retain their IDs, switches, prices and mechanics.
- Hood and Spurs use pre-hit hostile health/live proximity; Purse uses actual current gold; Ledger pays once at the true battle boundary, verifies the 5-gold delta and contributes damage only while equipped in that battle. Late equipping, insufficient funds or unrelated/duplicate money changes cannot create payment. Paid state survives re-equipping/restore without another charge.
- Scabbard reserves one physical opener, confirms real HP loss, queues one extra durability after contact and remembers the used weapon through drop/pickup/repair. Apron requires actual execution damage/death, not the start-animation RPC. Locket requires actual survived enemy HP loss below 25%, once per battle. Mitts uses the native backstab decision plus positive HP loss, one reward per enemy and ten-gold cap. Chalk samples actual aim/velocity and consumes on release. Hook confirms successful recovery of the player's own throw and repairs once per weapon, bounded by normal maximum.
- Foundation `NativeCombat` adds execution receipts, pre-hit backstab/target identity and spawn-generation tracking. Capturing target identity before damage handles death/despawn without a second reward identity. `NativeWeaponIdentity` transfers independent GUIDs through observed throw/drop/landing/embed/pickup/equip paths; equal native origins alone never identify a weapon. Runtime addresses are aliases, not serialized identities.
- `DirectHit.MultiplyDamage` / `Logic/DamageComposition` preserve Payback's flat addition when later equipment percentages apply. GameplayTick lets the pack clear aim outside Solo; runtime player-manager/count guards avoid native lobby error logs. Shared code contains no equipment-specific balance values.
- Validated `terms-and-contusions:equipment/battle-v1` state preserves paid/spent/per-enemy/per-weapon ledgers and bounded aim elapsed time. Capture refuses pending opener/payment confirmations and flushes extra durability first; restoration assigns state without payment/heal/gold/repair replay. Native equipment/history remains authoritative.
- Ten `[Equipment] Enable<Name>` startup switches default true; no new forced offers/cheats default on. See [all equipment, prices, hooks, persistence limits and manual matrix](terms-and-contusions/STAGE4_EQUIPMENT.md), plus [native evidence and shared API](foundation/STAGE4_EVIDENCE.md).

## Stage 5 implementation and prerequisite safeguards

- `Afflictions/{AfflictionDefinitions,AfflictionMath,AfflictionBattleState,AfflictionsFeature}.cs`: ten independent normal three-level definitions and severity 0–3 names/descriptions, native effective `HasInjury` factors and actual mechanics. Leadfoot's identity, behavior, aliases and optional force choice are preserved. Native potion generation, healing, selection and AddInjury/cure bookkeeping remain untouched.
- Lead Belly owns one additive recovery-timer tail, reconciled before the existing single Catch Your Breath clock; reads cannot compound it, and reduction/cure removes only its unconsumed part. Burning Lungs required a concrete prerequisite: normal **and haste** regeneration branches are scoped through a new default-neutral `RegenerationAmount` channel, leaving one-time/nested grants and the base rate getter unmultiplied.
- Brittle Guard uses one positive CompleteBlockByShield Execute-stage feedback, with extra raw loss before the native break/model/unequip path. Heavy Landing requires an actual dash transition/completion and skips its own movement modifier during the dodge. Shaky/Loose use confirmed enemy-caused HP loss and independent fixed cooldowns, with native resource/currency operations and clamps.
- Tin Ear scales only the player's new positive native favor/combo gains; it preserves accumulated favor, raw scores, pools, decay, timing and external ForceWild. Cold Start uses saved elapsed real-battle age. Obsessive Focus freezes prior focus per attributed action and deterministically chooses the next target among actual successful direct hits, using pre-despawn token/order. Area calculations retain attribution without becoming direct rewards. Overheating commits before payment, caps at three stacks, and invalidates cached costs after cure/suppression.
- Foundation `NativeActionEvents`, target/action-aware stat context (original constructor preserved), `DirectHit.TargetOrder`, `CombatServices` stamina/payment/shield/dodge/crowd events and `Logic/ContributionMath` supply only shared plumbing. Existing channel values and Stage 3/4 behavior are retained; new channels are appended.
- Validated `terms-and-contusions:afflictions/battle-v1` stores bounded ages/remaining times/counters/focus without gold/stamina/injury replay. A concrete bulk-cure prerequisite was resolved: native RemoveAllInjuries bypasses per-entry callbacks, so NativeOwnership now snapshots registered entries and reuses its transition gate for actual removal notifications. Pending state clears even before same-frame reacquisition; no-op/nested/failed/restore paths do not replay it. Other effective changes also refresh on use/tick. Native checkpoints omit live stamina/recovery and full arena objects: Lead Belly waits for new real exertion after reset/restore, and an unavailable focus is explicitly cleared/logged.
- Ten `[Afflictions] Enable<Name>` startup switches default true; no new forced offers/cheats default on. New text uses English fallback and native icon references. See [all eleven afflictions, choices and manual matrix](terms-and-contusions/STAGE5_AFFLICTIONS.md) and [native evidence/shared API](foundation/STAGE5_EVIDENCE.md).

## Stage 6 implementation and prerequisite safeguards

- `Surgeries/{SurgeryDefinitions,SurgeryBattleState,SurgeriesFeature}.cs`: all eleven surgery identities/text/prices and actual mechanics, including required Talons bleed; Adrenal Graft's ID 11020, free price, +25% recovery and twelve-language aliases retained. New IDs 11021–11030 are vacant in installed SurgeryType metadata. Ten positive prices are configurable; no balance claim.
- `Runtime/SurgeryServices.cs` and `Logic/SurgeryTransactions.cs`: one public shared provider adapter, combined eligible native/custom pool, donation/first-cure protection, requested-slot filling if native candidates are exhausted, deterministic sampling without Unity RNG changes, preserved legacy force choice, pre-payment rejection and once-per-run ownership. No competing per-feature replace-slot hooks or native surgery registry is invented.
- Genuine completion requires the same operation's native `PlayerData.ModifyAwardStat(AwardType.MostSurgeries,1)` int callback, normal surrounding RPC return, expected payment and absent prior ownership. Native payment/heal/codex/award/UI bookkeeping runs normally. Native `previousSurgeries` is authoritative and checkpointed; ownership is not inferred from a free money balance. The prior legacy completion heuristic is removed.
- `Runtime/NativeSurgeryStats.cs`, appended modifier channels and explicit regular/special and unarmed provenance implement actual HP/stamina/HUD, dodge distance/cost, knockback, healing and damage interactions. Resource percentages transition once when confirmed ownership changes the maximum. Normal getters/restore never grant resources. One-time stamina grants explicitly bypass passive-regeneration modifiers.
- Per-player schema-1 `terms-and-contusions:surgeries/battle-v1` stores Clot used/remaining budget, Counterweight/Fury remaining durations and independent bleed cadence. Validated restore assigns state without purchases, health changes or resource replay. Unsettled surgery/contact/damage dispatch refuses checkpoint capture. Vanished arena bleed targets are dropped/logged; native checkpoints do not reconstruct a full arena or current stamina.
- Talons uses native generated directionless Slash damage, five cadence ticks over three seconds, refresh without an immediate tick or parallel pair, and bounded catch-up after slow frames. The shared ordered native HP-dispatch queue prevents a generated/unrelated receipt from consuming another direct-hit/execution reservation. Counterweight waits through native recovery grace; Fury cooldown survives battle changes and suppresses positive Rage writes even while conversion is cooling down. Installed HarmonySupport/generic wrapper code was inspected without initializing the game; actual detours remain runtime-unverified.
- [Surgery mechanics, configuration, interaction and manual checks](terms-and-contusions/STAGE6_SURGERIES.md); [Foundation APIs and native evidence](foundation/STAGE6_EVIDENCE.md). All prior work, images, save data, loader and publishing/deployment restrictions are preserved.

## Stage 7 integration, community documentation and delivery

- Source checkpoint: `backups/expansion-stage7-20261026-061950`. The original complete spec stays byte-identical; all earlier uncommitted work and historical reports/screenshots are preserved.
- Reconciled every manifest row against the actual pure definitions/formulas and current native adapters. The [generated catalog](terms-and-contusions/CATALOG.md) maps all 46 IDs/keys/effects/prices/configs/source/state/removal/manual IDs; none is planned or a placeholder. All native runtime behavior remains unverified.
- Completed [separate plugin setup](foundation/SEPARATE_MOD.md), four end-to-end category tutorials with exact compiled example source, [mechanics/API/throw matrix](foundation/MECHANICS.md), [text/visual boundaries](foundation/LOCALIZATION_VISUALS.md), [installation/migration/rollback](INSTALLATION.md), full [manual checklist](MANUAL_TESTS.md) and retained MIT/dependency notices. Both optional example assemblies compile and are excluded from product delivery.
- Resolved integration prerequisites: attempt-wide Pick Your Moment includes attributed area damage without a second direct multiplier; Waste Not/Locket use explicit one-time clamped owner stamina grants; F1 adds a custom-only skill/injury catalog filter and clear rank/severity labels. Existing defaults, four IDs, GUIDs, net6.0 and tested loader remain unchanged.
- Final suite: **1,634 production-logic/catalog/metadata/tutorial checks + 34 synthetic deployment/rollback/archive checks = 1,668 nongame checks**, plus the explicit opt-in deployment guard. Four assemblies build Release/net6.0 with **0 warnings / 0 errors**. No live game handlers are called.
- Local Foundation-only, pack-only and combined ZIPs contain only intended project DLLs/README/license/notices/hash manifest. Actual deployment verified exactly two 0.8.0 production identities, Foundation then pack, and no old duplicate. Configs/saves are unchanged; backups, raw logs, generated bindings and binaries remain ignored.
- Final audit: 25 config/user-data files, five screenshots, four game binaries and exact spec unchanged; installed inventory 2/2/23, game closed; all three final ZIP payloads match installed hashes, 289 local documentation links resolve and ignored/private artifact policy holds.
- Exact artifact/installed hashes, deployment checkpoint, remaining limitations and final handoff: [STAGE7_HANDOFF.md](STAGE7_HANDOFF.md). No game launch, runtime test, Git commit/push, release or Thunderstore upload occurred. **The next action belongs to the user's manual Solo testing.**

## Historical final Stage 6 builds and checks

Command: `pwsh -NoProfile -File .\scripts\test-stage6.ps1`.

- Foundation, TermsAndContusions and StandaloneContentExample build **Release/net6.0 with 0 warnings / 0 errors**. Versions remain unreleased 0.8.0; loader and GUIDs unchanged.
- Prior checks remain passing: 50 + 111 + 521 + 191 + 271 = 1,144. **321 additional Stage 6 checks** exercise actual production eligibility/completion/payment/duplicate logic, all-candidate sampling and empty native pools, real modifier composition and proportional HP clamps, regular/special costs, Pain cap, Clot thresholds/over-time totals/remaining budget, Counterweight reservation/failure/expiry/recovery grace, Fury cooldown/battle/checkpoint continuity, Talons exact total/cadence/refresh/independent pairs/cleanup, ordered generated-vs-direct HP receipt routes, malformed-state rejection, all eleven manifest identities/prices, **21 unique installed native paths**, exact int award overload/MostSurgeries value and eleven native ID vacancies. **Total: 1,465 nongame checks**, plus the intermediate-deployment refusal check. No game assembly initialization or live handler invocation.
- Final integrity checks confirm all **26** installed/config/user-data files and **five** screenshots are byte-identical to the Stage 6 checkpoint; installed counts remain **1/2/23**, game is closed and GameAssembly/specification hashes are unchanged. Generated assemblies, backups, builds and raw logs remain ignored. Current local documentation links resolve and diff whitespace checks pass.

Final artifact SHA-256, **not deployed**:

| Artifact | SHA-256 |
|---|---|
| Foundation 0.8.0 | `f53fa5a611d11c73c1bc98e2fe93b29385c050d0b33baae247d5ed58c7f326a7` |
| TermsAndContusions 0.8.0 | `344c561c241542fbb2f0048444c4fdcec72942bc17cf10127e61cd0c55f5791f` |
| StandaloneContentExample 0.8.0 | `e80abe285a0417fe3024c3e2b370bddafd62802bc14b390548ad7c52156e881b` |

Stage 6 is complete as code/build/nongame verification. Runtime behavior remains awaiting the user's manual Solo tests after final deployment. The installed v0.7.3 baseline is unchanged. The historical Stage 1–5 results below are retained, not current artifact hashes.


## Prior Stages 1–5 builds and meaningful nongame checks

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-stage5.ps1
```

Final affected builds target `net6.0`: Foundation, TermsAndContusions and StandaloneContentExample, each **0 warnings / 0 errors**.

- **50 existing checks** pass: actual BepInEx configuration migration, GUID/dependency/assembly ownership, no copied reference DLLs, preserved IDs and complete manifest.
- **111 Stage 2 checks** pass: production registry including atomic multi-entry registration/failure rollback, checkpoint store/matching, failure/commit/pending recovery, older native checkpoint, independent original/copied rank/order/severity, feature/dormant state, schema rejection/preservation, migration, fresh/abandoned run cleanup, battle-scope dedup, canonical removal/reacquisition, no-op/nested/failed/restore events, proportional HP/clamps, modifier composition/removal, true Solo policy and standalone assembly boundaries.
- Included above: installed metadata checks for **30** unique core hook targets and all five native enum vacancies (four pack IDs plus example 12000), without loading game assemblies. Runtime hooks still resolve exact parameter types.
- **521 Stage 3 checks** pass: production rank/threshold/cap/HP/damage calculations, every rank x sling combination, inverse charge time and actual extra-clock logic, ballistic/preview calculations, current-next text, timed primes/reservoirs, reservations/idempotence, checkpoint rejection/restoration, battle/reset/removal, attempt deduplication, durability filtering, manifest entries, **22** unique Stage 3 targets and native ID vacancies.
- **191 Stage 4 checks** pass: actual equipment formulas/thresholds/caps and flat-aware composition; verified/failed/duplicate/late payment; opener success/failure/reservation/repair/removal; actual-enemy reward flags and cap; once-per-battle/weapon ledgers; checkpoint round trips and malformed-state refusal; aim timing/movement/release/removal/clock changes; explicit representation transfer and spawn-token deduplication; manifest prices/IDs and **19** unique installed method targets/new ID vacancies.
- **271 Stage 5 checks** pass: all 0–3 severity formulas/text, neutral suppression and cure, thresholds/intervals/resource clamps, contribution-only favor scaling, delay reconciliation/composition, regeneration and movement composition, partial shield counters, fixed damage cooldowns, landing/Cold Start remaining time, heat cap/pre-payment/reset/cure epochs, callback-order-independent focus, checkpoint round trips/malformed-state refusal, manifest identities and **18** unique installed native paths/new InjuryType vacancies. Total across Stages 1–5: **1,144 nongame checks**.
- One additional workflow check confirms intermediate deployment refuses installation before any copy/migration.
- All **91 current local documentation links** resolve and diff whitespace checks pass. Ignored binaries/backups/raw investigation files remain outside the Git change set. Final SHA-256 comparisons against the Stage 5 checkpoint confirm all 26 installed/config/user-data files, the five screenshots, GameAssembly and complete specification are unchanged; counts remain 1/2/23 and the game is closed.
- Checkpoint tests use temporary synthetic native-shaped JSON and the actual pure production source files; no game or live handler is initialized. An initially outdated empty-legacy fixture was corrected to use its descriptor's true key after identity validation was strengthened; the final suite passes.

Build artifact hashes, **not deployed**:

| Artifact | SHA-256 |
|---|---|
| Foundation `0.8.0` | `0e3c9fc2d0dd375cc87f539c04da71f198054543ddfedfd6d5ac9c84c5354356` |
| TermsAndContusions `0.8.0` | `d5f1035f0d1ab681c7df6470f22c35588475b550720fa8472aa6eb48bb59de0a` |
| StandaloneContentExample `0.8.0` | `d69facce736dccec9a157927820eeff8d0ffbcc6bc154edc987f9e5c926a79d8` |

Hashes are from the final passing Stage 5 build. Prior stage artifacts are retained in backups; the installed DLL is still v0.7.3.

## Limitations and manual verification

Actual split chainloader/dependency startup, live I2 terms, IL2CPP callback interception, native JSON serialization, async write/commit, preview/restore ordering, older checkpoints, missing-content refusal and HUD/resource behavior remain **awaiting manual gameplay testing after final deployment**. Builds and metadata checks do not establish these runtime results.

Stage 3's native callback attribution, damage/resource behavior, current/next card fit, actual throw distance/preview/targeting and inverse charging likewise remain unverified. Normal held weapons/objects/limbs using ThrowWeapon are supported; bow firing, offhand abilities, thrown enemies/grapples and independent traps/generated launches use other paths and do not receive normal-throw range/charge bonuses. Native collisions, random angles, special objects and highest-speed physics need manual testing. Numbered coroutine bindings are specific to the inspected game build.

Stage 4 likewise requires manual execution/backstab/HP authority/ordering, paid and spent behavior, real extra durability/breaks, physical transfer through normal/dropped/embedded weapons, shop/card layout and save/resume checks. Native player/run checkpoints do not serialize arena weapon/pickup/projectile/enemy instances: this is not a complete arena snapshot. Existing live objects retain runtime tokens during same-process restore; objects newly constructed by native resume are new physical spawns. Saved battle caps/paid/spent flags remain preserved, without resource replay. Cross-process reconstruction of vanished physical objects is not claimed.

Stage 5 requires manual normal-potion healing/acquisition, native effective suppression/reduction/cure, actual stamina/gold/HP authority, shield/dodge feedback, crowd progression/UI, focus/heat multi-target/payment ordering and save/resume verification. Native live stamina/recovery is not checkpointed, so the plugin does not replay a Lead Belly tail onto a reconstructed timer. Vanished arena focus is cleared explicitly; no full arena/stamina snapshot is claimed. The guide records intentional focus target selection, ranged-chain break, favor fields and existing Locket-before-Shaky event ordering.

The persistence adapter supports Solo/default SKU on the inspected Windows Steam/Standalone paths; multiplayer and automatic Steam Cloud sidecar synchronization are not implemented. Native SaveData and matching sidecar archives must be backed up/restored together. Archives are intentionally retained for older native backups; reset/abandon clears live state and cannot select an archive by seed alone. There is no automatic archive garbage collector.

Stage 6 replaces Adrenal Graft's money-only completion heuristic and one-in-four visit gate with genuine native completion receipts and a shared combined eligible pool. Actual IL2CPP completion/UI ordering, closed generic Fury detours, HP/stamina authority and physics, Talons timing/mitigation/cleanup, and save/resume remain manually unverified. Surgery rerolls still animate without changing offers in the user's test; same-visit deterministic sampling does not claim to fix them. Potion replacement may produce existing animation warnings.

Existing twelve-language pack aliases and all five earlier screenshots are preserved; Bulk Up's translated description now reflects four ranks. **New skill/equipment/affliction/surgery and dynamic rank text use English fallback.** Translations are not native-speaker reviewed. Original icon import is not implemented; native visual references are reused. Content remains deliberately unbalanced technical examples.

## Backups and next concrete stage

- Stage 1 start: `backups/expansion-stage1-20261025-121742`.
- Stage 2 start: `backups/expansion-stage2-20261025-124225` — preserves Stage 1's uncommitted source/docs/scripts/tests plus deployed DLL, configs, user data, source commit/patch and SHA-256 manifest.
- Stage 3 start: `backups/expansion-stage3-20261025-165045` — preserves the prior source/docs/scripts/tests/examples and Stage 2 build artifacts, deployed plugin, configuration, user data, commit/patch and SHA-256 inventory outside loader scan paths.
- Stage 4 start: `backups/expansion-stage4-20261025-182500` — preserves the complete pre-change Stage 3 working tree/build artifacts plus the installed plugin, configuration, user data, commit/patch and SHA-256 inventory. Nothing was deployed or migrated.
- Stage 5 start: `backups/expansion-stage5-20261025-185657` — preserves the complete pre-change Stage 4 working tree/build artifacts, deployed v0.7.3, configuration, user data, commit/patch and SHA-256 inventory outside loader scan paths. No game launch or deployment occurred.
- Stage 6 start: `backups/expansion-stage6-20261025-200242` — complete pre-change Stage 5 uncommitted source/builds, deployed DLL, configs, full user data, commit/patch and SHA-256 manifest; all prior backups preserved.
- Stage 7 actually replaced the old DLL with both production plugins; use the exact deployment checkpoint in [the handoff](STAGE7_HANDOFF.md) to restore DLLs/configuration and the entire corresponding user-data backup together. Old v0.7.3 cannot read the new discriminator/checkpoint protocol. See [migration/rollback](foundation/CONFIGURATION_MIGRATION.md) and [paired-save rules](foundation/PERSISTENCE.md).

**Next: the user's manual Solo test, using [MANUAL_TESTS.md](MANUAL_TESTS.md), then targeted fixes for evidenced failures.** All seven planned implementation stages are complete; no Stage 8 is automatically started. Nothing is published or gameplay verified.

Stage 7 stops here.
