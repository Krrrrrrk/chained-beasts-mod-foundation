Implement the next major expansion of this repository: a reusable Chained Beasts Mod Foundation and a separate content pack named **Terms & Contusions**.

Do the implementation, documentation, builds, and local deployment. Work through the full scope below rather than stopping after a plan or a small demonstration.

## 1. Project context and working rules

Existing project:
`D:\chained-beasts-modding`

Repository:
`https://github.com/Krrrrrrk/chained-beasts-mod-foundation`

Game:
`C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts`

User data:
`C:\Users\Krrrrrrk\AppData\LocalLow\Featherweight Games\ChainedBeasts`

Inspect the latest local working tree, existing instructions, current implementation, reports, and installed game references first. Preserve newer local work and uncommitted changes. Do not reset the repository to an earlier snapshot.

Continue using the working BepInEx 6 IL2CPP setup and existing build approach unless a specific requirement makes a change necessary. Verify APIs against the actual installed interop assemblies and inspected game behavior; do not invent game methods or assume generated dummy method bodies establish runtime behavior.

Back up the existing source state, deployed plugin DLLs, configuration, and user data before deployment or migration work. Store backups outside directories that BepInEx scans for plugins.

**I will perform gameplay testing manually.**

Do not launch the game, automate menus, invoke live purchase/grant/gameplay handlers, take screenshots, or claim that a successful build establishes gameplay correctness.

If the game is running, finish the source work and build without deploying, then tell me to close it. Do not terminate it.

Prepare repository changes and distributable packages locally. This prompt does not newly authorize a GitHub push, release publication, or Thunderstore upload. Follow any publishing authorization I have already explicitly given in this Codex session.

## 2. Exact scope

Implement all 40 content ideas specified below, plus:

- **Longshot:** a skill that increases throw range and accelerates throw charging.
- **Counterweight Sling:** equipment that provides the same rank-one effect and stacks with Longshot.

Preserve the four existing additions:

- Bulk Up.
- Runner’s Wraps.
- Leadfoot.
- Adrenal Graft.

The production content pack must therefore contain:

| Category | New entries | Existing entries | Total |
|---|---:|---:|---:|
| Skills | 11 | 1 | 12 |
| Equipment | 11 | 1 | 12 |
| Potion afflictions | 10 | 1 | 11 |
| Surgeries | 10 | 1 | 11 |
| **Total** | **42** | **4** | **46** |

Do not implement the four ideas reserved for later:

- Recall Ring.
- Debt Collector.
- Bowling for Beasts.
- Blood Bank Flask.

**Retractable Talons is part of the included 40. Implement its bleed effect; it is not one of the deferred ideas.**

A definition, tooltip, offer entry, or placeholder handler is not a completed content entry. Each entry needs its actual mechanics, lifecycle, presentation, acquisition, and persistence where applicable.

These values are experimental starting values. Implement the specified mechanics faithfully and document balance as experimental.

## 3. Two clear products in one repository

Organize the repository around two substantial, clearly separated areas.

### Chained Beasts Mod Foundation

This is the reusable infrastructure and teaching material for other modders.

It should provide the shared capabilities actually needed by the completed content:

- Content registration and collision detection.
- Localization and presentation integration.
- Stat modifiers and the required gameplay event integration.
- Player, run, battle, and equipment lifecycle handling.
- Persistent custom-content state.
- Configuration support.
- Optional developer tools.
- Public extension points that another plugin can use.

Preserve the existing Foundation plugin GUID and assembly identity where practical.

The Foundation must load independently without automatically enabling Terms & Contusions content.

### Terms & Contusions

Create a distinct content-pack project and plugin, with its own assembly name, plugin GUID, configuration identity, and explicit versioned dependency on the Foundation.

Move the four existing gameplay examples into this pack alongside the 42 new entries. Preserve their established numeric IDs:

- Bulk Up skill: `11000`.
- Runner’s Wraps equipment: `2499`.
- Leadfoot injury: `11010`.
- Adrenal Graft surgery: `11020`.

Ensure the original content is registered and patched exactly once. Do not leave its previous implementations active in Foundation while also loading the pack versions.

Preserve existing configuration choices through an intentional migration where needed. Existing saved ownership must remain meaningful after this split.

A suitable layout would include:

- `src/ChainedBeasts.ModFoundation/`
- `src/TermsAndContusions/`
- `examples/StandaloneContentExample/`
- `docs/foundation/`
- `docs/terms-and-contusions/`

Adjust exact paths to fit the existing repository cleanly.

“Half Foundation, half content pack” means clear ownership, navigation, and substantial documentation for both. It does not require an artificial 50/50 file count.

Keep the public API practical. The four native content categories have different registration and acquisition paths; preserve those differences where necessary.

## 4. Solve persistence before expanding the catalog

The existing Bulk Up and Leadfoot implementations use individual reserved native checkpoint slots. That approach must not be copied across dozens of new features.

Implement one versioned persistence mechanism capable of saving multiple independently identified custom skills, afflictions, surgeries, and other necessary state together.

Inspect the actual native checkpoint/save paths before choosing the implementation. Do not assume native serialization automatically includes arbitrary added fields or that custom enum values can index native fixed-size arrays.

The persistence design must cover:

- Stable, namespaced content keys and numeric-ID mappings.
- Correct run and player identity.
- Association with the corresponding native checkpoint.
- Skill ranks and acquisition order.
- Affliction severity and order.
- Surgery ownership.
- Persistent feature state where necessary.
- Correct restoration of an older checkpoint, not merely the most recently written mod state.
- Fresh-run and abandoned-run cleanup.
- Missing or disabled content and unknown schema versions.
- Migration from the existing Bulk Up and Leadfoot marker slots.

If a sidecar is the appropriate solution, use atomic writes, associate data with the successful native checkpoint operation, and handle mismatched or stale sidecars explicitly. Document backup and restoration requirements.

Persistent identities must not depend on process pointers or unstable registration order.

Import legacy state only once. Restoring or migrating ownership must not replay healing, purchases, gold charges, or other acquisition effects. Existing rank-one Bulk Up saves must retain their current rank-one behavior.

Keep native ownership/history paths authoritative where they already support the required data. Avoid two competing sources of truth.

Fix the Bulk Up removal/reacquisition bookkeeping issue across normal game removal and checkpoint restoration paths, not only the developer menu.

Also investigate and fix the documented full-screen acquisition-banner issue so custom skills show their own text consistently.

## 5. Skill ranks and modifier composition

All **12 custom player skills** support four meaningful ranks.

Preserve vanilla skill rank limits. Equipment does not gain skill ranks. Afflictions remain at one to three skulls. Surgeries remain once per player per run.

Let `r` be the actual owned skill rank, clamped to `0..4`.

Use the exact rank behavior in the table below. Rank zero contributes no effect. Thresholds, durations, distances, and cooldowns stay fixed unless explicitly stated.

Within one skill, ranks increase the contribution according to its formula. Independent percentage multipliers from different sources combine multiplicatively unless a specific mechanic requires another rule.

Calculate effects from current ownership, equipment, effective severity, and otherwise-normal values. Property reads and UI refreshes must never compound a previous result.

### Skill definitions

| Skill | Required behavior |
|---|---|
| **Bulk Up — existing** | Maximum-health contribution is `1 + r`: **2× / 3× / 4× / 5×** at ranks 1–4. On a genuine rank change, adjust current HP proportionally using the old and new effective maxima, respecting native attainable-health limits. Restoration must not grant the adjustment again. |
| **Measured Violence** | Deal **25% × r more melee damage** when stamina is at least **80%** immediately before the attack pays its cost. |
| **Running on Fumes** | Deal **35% × r more melee damage** when stamina is below **25%** immediately before attack payment. |
| **Catch Your Breath** | While stationary, the eligible stamina-regeneration delay becomes `otherwiseNormalDelay / (1 + r)`. Rank one halves the delay; rank four reduces it to one-fifth. Recovery rate is unchanged. |
| **Expensive Taste** | Damage bonus is `r × min(0.01 × floor(max(gold, 0) / 10), 0.25)`. Caps are **+25 / +50 / +75 / +100%**. Recalculate from actual gold. |
| **Fresh Meat** | Deal **30% × r more direct attack damage** against an enemy at full effective health immediately before the hit. |
| **Pick Your Moment** | After **two seconds without attacking**, the next committed attack deals **50% × r more damage**. Consume the charge on the attempt, including a miss. One charge maximum. |
| **Mixed Martial Arts** | A successful direct melee hit primes the next direct thrown-weapon hit for **30% × r extra damage**; a thrown-weapon hit primes the next direct melee hit for the same bonus. Each primed bonus lasts **six seconds** and does not stack with itself. |
| **Payback Plan** | Store **50% × r of actual enemy-caused HP loss**, capped at **25% of maximum HP at every rank**. Higher ranks fill the same bounded reservoir faster. Expire it five seconds after the latest qualifying damage. Add the stored amount once to the next successful direct melee hit, then clear it. |
| **Waste Not** | A weapon breaking through normal durability loss restores **25% × r of maximum stamina**: 25/50/75/100%. Clamp to the normal stamina maximum. Trigger once per genuine break. |
| **Breathing Room** | Damage reduction is `min(0.05 × r × nearbyEnemyCount, 0.20 × r)`, counting living hostile enemies within **three metres**. Maximum reduction from this skill is **20/40/60/80%**. |
| **Longshot — new** | Throw-range multiplier and throw-charge-speed multiplier are both `1 + r`. See the dedicated stacking specification below. |

Update all descriptions to show current and next-rank effects accurately.

Bulk Up’s old description alone becomes misleading above rank one. Explain its cumulative rank bonus and proportional current-HP adjustment.

Normal trainer generation, ownership checks, costs, and maximum-rank handling must recognize all four ranks and prevent a fifth acquisition.

## 6. Longshot and Counterweight Sling

**Longshot** is a normal trainer skill.

**Counterweight Sling** is a new equipment item that modifies the player’s normal throws. It does not require a separate ammunition system or a new throwable weapon type.

At rank one, Longshot gives:

- **2× effective throw range.**
- **2× throw charging speed**, meaning half the normal charge time.

The sling independently gives:

- **2× effective throw range while equipped.**
- **2× throw charging speed while equipped.**

Let `s = 2` while the sling is equipped, otherwise `s = 1`.

Required formulas:

- `rangeMultiplier = (1 + r) × s`
- `chargeSpeedMultiplier = (1 + r) × s`
- `chargeTime = otherwiseNormalChargeTime / chargeSpeedMultiplier`

| Longshot rank | Range and charge speed without sling | Range and charge speed with sling |
|---:|---:|---:|
| 0 | 1× | 2× |
| 1 | 2× | 4× |
| 2 | 3× | 6× |
| 3 | 4× | 8× |
| 4 | 5× | 10× |

Rank one plus the sling therefore gives four times the range and one-quarter the charge time. Rank four plus the sling gives ten times the range and one-tenth the charge time.

Inspect how the game actually calculates throw range, charge, trajectory, targeting previews, and any relevant clamps. Implement the promised change to actual throws; changing only a displayed number is insufficient.

Do not assume a particular launch-speed multiplier produces the same multiplier in actual range. Keep targeting previews consistent with real throws wherever the native preview is normally shown.

Apply charge acceleration at one canonical point. Do not independently accelerate progress and divide duration in a way that applies the bonus twice.

Compare range at the same normalized charge under comparable conditions. Preserve unrelated native throw modifiers, collisions, damage, stamina costs, and object behavior.

Support ordinary held weapons and throwable objects using the normal throw mechanic. Identify genuine exceptions in the documentation.

Removing the sling or a skill rank removes only that source’s contribution. Changing equipment or ranks during charging must not create a free completed charge or duplicate a throw.

Use a distinct equipment ID and definition. Any reused visual reference must not bring along another item’s gameplay identity.

## 7. New equipment

These ten items are in addition to Counterweight Sling and the existing Runner’s Wraps.

| Equipment | Required effect |
|---|---|
| **Executioner’s Hood** | Deal **35% more melee damage** to enemies below **30% effective health**, evaluated before the hit. |
| **Lead-Lined Purse** | Take **1% less damage per 10 gold carried**, capped at **20% reduction**. Spending gold immediately changes the contribution. |
| **Borrower’s Scabbard** | A weapon at full durability deals **50% extra damage on its first successful hit**, and that hit consumes one additional durability. |
| **Butcher’s Apron** | A confirmed execution heals **3% of maximum HP**. |
| **Last-Gasp Locket** | Once per battle, the first enemy-damage event that leaves the player alive below **25% HP** restores **50% of maximum stamina**. |
| **Mercenary’s Ledger** | If equipped at battle start, pay **5 gold** for **20% more damage during that battle**. Insufficient funds means no payment and no bonus. |
| **Pickpocket’s Mitts** | The first successful backstab against each enemy grants **1 gold**, with a **10-gold cap per battle**. |
| **Chalk Bag** | Aim while stationary for **1.5 seconds** to empower the resulting throw with **40% extra damage**. Movement cancels preparation; releasing the throw consumes it. |
| **Salvager’s Hook** | Recovering a weapon the player previously threw repairs **one durability**, once per weapon per battle, without exceeding its normal maximum. |
| **Duelist’s Spurs** | Deal **20% more melee damage** when the target is the only living hostile enemy within **four metres** of the player. |

Choose sensible initial prices by inspecting comparable native equipment, record them, and make them straightforward to tune. Do not pause for price approval.

Equipment rules:

- Effects apply only while equipped.
- Unequipping and re-equipping cannot reset per-battle allowances, per-enemy rewards, or per-weapon limits.
- Borrower’s Scabbard has one opening-hit allowance per physical weapon per battle. Repairing it to full, dropping it, or picking it up again does not rearm that allowance during the same battle.
- A paid Mercenary’s Ledger bonus is active while the Ledger is equipped. Re-equipping during the same battle can restore that already-paid bonus without another charge.
- Equipping the Ledger after battle start does not create a free bonus or retroactively trigger payment.
- Confirm real executions and weapon breaks; repeated callbacks, animation events, drops, and despawns must not award duplicate benefits.

Preserve Runner’s Wraps’ existing identity and behavior.

## 8. New potion afflictions

All ten preserve the generated potion’s normal healing and use normal potion acquisition, injury application, reduction, and cure paths.

Effects depend on effective severity after native suppression or reduction. Curing or reducing an affliction updates its effect and any related pending state.

Values separated by slashes represent one, two, and three skulls.

| Affliction | Required effect at 1 / 2 / 3 skulls |
|---|---|
| **Lead Belly** | Stamina regeneration starts **0.25 / 0.5 / 0.75 seconds later** after exertion. Recovery rate is unchanged. |
| **Brittle Guard** | A blocked hit consumes one extra shield durability on every **fourth / third / second blocked hit**. |
| **Shaky Resolve** | Enemy damage also drains **5 / 10 / 15% of maximum stamina**, at most once per second. |
| **Tin Ear** | The player’s actions generate **15 / 30 / 45% less crowd favor**. |
| **Burning Lungs** | Stamina regenerates **20 / 35 / 50% slower while moving**. It recovers at the normal rate while stationary. |
| **Heavy Landing** | After a dodge, movement is **20 / 30 / 40% slower for 0.75 seconds**. The dodge itself retains its normal behavior. |
| **Loose Change** | Enemy damage subtracts **1 / 2 / 3 gold**, at most once every three seconds. Never reduce gold below zero. No new physical coin pickups are required. |
| **Cold Start** | Deal **10 / 20 / 30% less damage during the first ten seconds** of a battle. |
| **Obsessive Focus** | After directly damaging an enemy, attacks against other enemies deal **15 / 25 / 35% less damage for three seconds**. |
| **Overheating** | Consecutive melee attacks add **5 / 10 / 15% stamina cost per stack**, up to three stacks. Two seconds without attacking clears the stacks. |

For Overheating, the first attack after the two-second gap has normal cost. Each subsequent attack within the window adds one stack before its cost is paid, up to three.

For Obsessive Focus, evaluate a hit against the previous focus before updating the focus. Handle multiple targets struck by one action consistently, rather than letting callback order produce accidental bonuses or penalties.

Shaky Resolve and Loose Change require actual qualifying enemy damage, not merely an attempted hit that dealt no HP damage.

Preserve Leadfoot’s existing identity, severity behavior, and normal potion healing.

## 9. New surgeries

Implement all ten as normal surgeon options, purchased once per player per run and lasting for that run.

Choose documented, sensible nonzero starting prices using comparable native operations. Keep costs easy to tune.

| Surgery | Required effect |
|---|---|
| **Anchor Heels** | Receive **75% less knockback**, but dodges travel **20% less distance**. |
| **Spring-Loaded Knees** | Dodges travel **30% farther**, but cost **20% more stamina**. |
| **Dense Skeleton** | Take **15% less damage**, but maximum stamina falls **20%**. |
| **Auxiliary Tendons** | Regular melee attacks cost **20% less stamina**, but maximum HP falls **10%**. |
| **Marrow Enrichment** | Receive **30% more healing**, but maximum HP falls **15%**. |
| **Pain Converter** | Enemy damage restores stamina equal to **1.5 times the fraction of maximum HP actually lost**, capped at **25% maximum stamina per hit**. Maximum stamina falls **10%**. |
| **Emergency Clotting** | Once per battle, survive an enemy hit that removes at least **20% of maximum HP** to recover **25% of that hit’s actual HP damage over four seconds**. |
| **Counterweight Implant** | After recovering from a stun or trip, the next melee hit within five seconds deals **25% more damage and 75% more knockback**. One pending charge maximum. |
| **Fury Pacemaker** | Prevent future fury gains. Instead, an eligible fury grant restores **30% of maximum stamina**, at most once every eight seconds. |
| **Retractable Talons** | Direct unarmed hits apply a bleed dealing **5 total damage over three seconds**. Repeated hits refresh the bleed rather than stacking copies. Weapon damage falls **10%**. |

Pain Converter example: losing 10% of maximum HP restores 15% of maximum stamina, before the per-hit cap and normal stamina cap.

Fury Pacemaker still prevents fury during its conversion cooldown. Do not turn repeated callbacks for one fury event into multiple stamina grants or recursively trigger fury effects.

For Retractable Talons, use one bleed per attacker/target pair. Refreshing renews duration without creating parallel copies or an extra immediate tick. Bleed ticks must not count as new direct unarmed hits.

Confirm genuine surgery completion before granting ownership. An executing RPC or unchanged money alone is not enough to prove that a free or rejected operation completed.

Use a shared, intentional surgery-offer integration for the whole catalog. Do not install a separate “replace the same slot” postfix for every surgery and let the last hook win.

Preserve native payment, eligibility, UI, and bookkeeping. Document how custom surgeries participate in offers when no native registry exists.

Preserve the existing Adrenal Graft ID and effect; keep its existing price unless there is a documented reason to change it.

## 10. Shared behavior and interaction rules

Use shared services for repeated concerns while keeping individual feature logic understandable.

### Stat and event handling

- Capture stamina-dependent attack conditions before attack costs are paid.
- Capture target-health conditions before that hit changes target HP.
- Distinguish direct attacks from bleed ticks and other generated effects.
- Use actual HP lost for damage-based effects; exclude overkill and attempted damage.
- Enemy-damage triggers must exclude self-inflicted, environmental, and friendly damage unless a specification says otherwise.
- Tag generated effects so they cannot recursively trigger themselves.
- Tie triggers to the correct player, target, weapon, attack, battle, and run.
- Handle nested RPC and duplicate callback paths so a single event is applied once.
- Define multi-hit and multi-target behavior deliberately.
- Clamp healing, stamina restoration, durability repair, and currency operations appropriately.

For Mixed Martial Arts, one eligible hit consumes its applicable primed bonus and primes the opposite category. Generated damage cannot create an alternating-hit loop.

For Payback Plan, consume the stored amount once. Do not repeatedly recalculate and add the same stored damage during one attack.

### Lifecycle

- Rank removal and native skill removal immediately update effects and acquisition bookkeeping.
- Equipment removal removes its active modifiers without resetting consumed allowances.
- Scene changes and UI refreshes do not count as new battles.
- Loading ownership does not replay resource grants or purchases.
- Timers and charges reset at the correct lifecycle boundaries.
- Restore the appropriate state when a checkpoint represents an existing battle or run.
- Health-changing surgery and skill transitions must handle current HP consistently and avoid double-applying native adjustments.

### Solo scope and developer tools

Maintain honest solo support until multiplayer is separately verified.

Follow the game’s actual authority for each modified value; do not apply a blanket rule that every hook must run only on the host.

Enforce an actual solo-session predicate for developer-menu actions and debug overrides. A host check alone is insufficient. Clear temporary cheats when leaving solo mode.

Keep developer tools useful for manually testing the larger catalog: clear content names, ranks/severity, and category selection. Preserve existing user configuration choices.

Forced offers and cheats must not be enabled accidentally by new defaults. Testing overrides should select a specific content entry and preserve the normal purchase/acquisition interaction.

## 11. Detailed community documentation is a required deliverable

The root README must give visitors two prominent routes:

1. **Play Terms & Contusions**
2. **Make your own mod with Chained Beasts Mod Foundation**

Write substantial, practical documentation based on the implemented code.

### Foundation getting started

Explain:

- What Foundation supplies and what a content plugin supplies.
- The supported game and loader versions.
- Windows, PowerShell, .NET, BepInEx, and generated-interop prerequisites.
- A fresh checkout, portable game-directory configuration, build, and installation.
- Expected logs and how to diagnose dependency or registration failures.
- How Foundation can be installed without Terms & Contusions.

### Making a separate mod

Provide a small buildable standalone example plugin, excluded from normal deployment, with distinct sample identities.

Walk through:

- Creating the project and referencing Foundation.
- Choosing an assembly name, namespace, plugin GUID, display name, and version.
- Declaring the Foundation dependency.
- Choosing namespaced content keys and valid numeric IDs.
- Adding new constants without modifying existing content identities.
- Registering during the correct lifecycle stage.
- Building, installing, and coexisting with Terms & Contusions.
- Packaging the resulting plugin.

The sample must use the actual public API and compile against it.

### A complete tutorial for each content category

Provide an end-to-end walkthrough for:

- A custom skill.
- Custom equipment.
- A potion affliction.
- A surgery.

Each tutorial must explain the definition, registration, localization, visual references, offer eligibility, acquisition, effect, removal, persistence, and manual verification.

Use exact filenames, actual methods, complete relevant code examples, and explanations of why each hook or extension point exists. A reader should not need the earlier private reports or our conversation.

### Mechanics and persistence

Document:

- Four-rank skill scaling.
- Combining skill, equipment, surgery, and affliction modifiers.
- The complete Longshot/sling matrix.
- Stat reads versus one-time events.
- Multi-hit handling and damage provenance.
- Per-battle and per-weapon state.
- Independent save keys and content identities.
- The persistence format and legacy migration.
- Checkpoint rollback, missing content, disabled features, and uninstall limitations.
- Coexistence with another Foundation-based mod.

Explicitly explain why copying the old reserved-slot bridges is insufficient for independent content.

### Localization, visuals, and compatibility

Explain text-term registration, current/next-rank descriptions, severity text, offer cards, acquisition banners, and character-sheet presentation.

Document asset reuse from the installed game and the support status for original icons or assets honestly. Do not imply an original-asset workflow works unless implemented and verified.

Preserve existing screenshots and label their context. I will supply new gameplay screenshots after testing.

Explain the supported API boundaries and which new mechanics still require game-specific investigation.

### Terms & Contusions documentation

Provide a complete catalog of all 46 entries, including:

- Category and stable identity.
- Effects and exact scaling.
- Prices, thresholds, caps, cooldowns, and durations.
- Known interactions and intended combinations.
- Configuration.
- Acquisition and removal behavior.
- Save/uninstall requirements.
- Implementation and verification status.

Keep documentation synchronized with the actual implementation. Clearly distinguish the last manually tested version from this newly built version.

Preserve MIT licensing for original project code and documentation and retain appropriate third-party notices. Keep game binaries, generated interop, extracted assets, decompiled game code, user saves, and private logs out of the repository and packages.

## 12. Verification without launching the game

Build both production projects and the standalone example.

Use meaningful nongame tests where possible for real production logic, including:

- Rank-zero through rank-four calculations.
- Every cell of the Longshot/sling matrix.
- Correct inverse relationship between charge speed and charge duration.
- Independent modifier removal.
- ID and key collision detection.
- Persistence round-tripping multiple skills, afflictions, and surgeries together.
- Legacy migration.
- Matching and mismatched checkpoint restoration.
- Removal/reacquisition bookkeeping.
- Equipment allowance and payment lifecycle logic.

Exercise the implementation’s actual pure logic rather than separate test-only copies of its formulas.

Prepare a targeted manual gameplay checklist for me covering:

- All 46 entries’ presentation, acquisition, and actual effects.
- Ranks one through four and prevention of rank five.
- Bulk Up acquisition, normal removal, reacquisition, and save/restore.
- Several custom skills and afflictions saved together and restored independently.
- Actual throw distance and full-charge time with Longshot ranks 0–4, with and without the sling.
- Sling removal and changes while charging.
- Runner’s Wraps with Spring-Loaded Knees.
- Bulk Up with Marrow Enrichment and Butcher’s Apron.
- Gold-dependent effects and Loose Change.
- Mixed Martial Arts with Borrower’s Scabbard.
- Fury Pacemaker with native fury-generating skills.
- Retractable Talons’ total damage, refresh behavior, and cleanup.
- Equipment swapping and repeated UI/scene transitions.
- Fresh-run reset and original-content regression checks.

Do not execute these gameplay checks yourself.

## 13. Build, deploy, and handoff

Update the build/deploy workflow for the two-plugin arrangement and explicit dependency order.

With the game closed, deploy only the intended project DLLs. Do not copy referenced game, Unity, BepInEx, or interop assemblies into the plugin package. Avoid leaving an older content-bearing Foundation DLL installed alongside the new one.

Prepare local packages and installation instructions that clearly explain which files players need for Foundation and Terms & Contusions.

Provide:

- Updated source and documentation.
- Successful build results and deployed DLL hashes.
- A complete content manifest accounting for **12 skills, 12 equipment items, 11 afflictions, and 11 surgeries**.
- A report mapping every entry to its implementation, registration, rank/severity handling, persistence, and manual test.
- Migration details and rollback instructions.
- A concise list of remaining limitations and genuinely unverified behavior.

Implement the entire requested pack. Use internal stages to keep the work manageable, and continue through them without routine approval pauses.

Do not silently omit difficult entries or label placeholders as implemented. If an actual engine limitation blocks a specific mechanic, investigate it, explain the concrete blocker, complete unaffected work, and identify precisely what remains.

Change the execution plan for the Chained Beasts Mod Foundation / Terms & Contusions expansion to staged implementation.

The full expansion specification remains authoritative for content, formulas, identities, compatibility, documentation, and final deliverables. This message changes how we execute it.

**Override the earlier instruction to implement the entire expansion continuously without stopping. Complete one implementation stage per turn, then stop with a handoff.**

## First, preserve the specification and reconcile existing work

1. Inspect the current working tree and any existing progress reports. Preserve uncommitted changes and valid completed work. Do not restart or undo work simply to match this stage order.
2. Save the complete original expansion specification as `docs/EXPANSION_SPEC.md`, or reuse an existing equivalent file. Preserve the exact content definitions, formulas, exceptions, and requirements. Do not replace the specification with a summary.
3. Maintain `docs/EXPANSION_PROGRESS.md` as a concise checklist recording:
   - Each stage’s status.
   - Implemented features and relevant source files.
   - Build and automated-check results.
   - Outstanding problems and dependencies.
   - Gameplay behavior still awaiting my manual testing.
   - The next concrete task.
4. Maintain a content manifest accounting for all **46 entries: 12 skills, 12 equipment items, 11 potion afflictions, and 11 surgeries**.

## Implementation stages

### Stage 1 — Separate the two plugins

Establish the Foundation and Terms & Contusions projects and their dependency relationship.

Move the four existing gameplay additions into Terms & Contusions while preserving their IDs, behavior, configuration choices, and compatibility requirements. Ensure they are registered and patched only once.

Foundation must be able to load independently without enabling the content pack.

Build both projects. Do not add the new content catalog during this stage. Record any compatibility work that must be completed in Stage 2 before deployment.

### Stage 2 — Persistence and shared infrastructure

Implement the versioned persistence system, checkpoint association, legacy migration, stable content identities, collision detection, and necessary ownership/lifecycle handling.

Establish the shared modifier and event handling required by the implemented features. Address Bulk Up’s removal/reacquisition/restoration bookkeeping and investigate the acquisition-banner issue.

Create a minimal buildable standalone example using the actual public API.

Add meaningful nongame checks for persistence, migration, checkpoint matching, and the shared production logic. Extend the infrastructure in later stages when a concrete mechanic requires it.

### Stage 3 — Skills and the throwing interaction

Complete all 12 custom skills, including four-rank support and the specified Bulk Up changes.

Also implement Counterweight Sling in this stage so Longshot and the sling can be evaluated together.

Check the full rank/stacking matrix and inverse charge-time calculations using production logic. Record actual throw distance, targeting behavior, and charging behavior as requiring manual gameplay verification.

### Stage 4 — Remaining equipment

Complete the remaining equipment so the catalog contains all 12 items.

Implement the specified per-battle, per-enemy, and per-weapon allowances, payment handling, equipment removal, and re-equipping behavior.

Reuse the Counterweight Sling implementation from Stage 3.

### Stage 5 — Potion afflictions

Complete all 11 afflictions, including the preserved Leadfoot implementation.

Cover effective severity, suppression, reduction, cure behavior, persistence, and preservation of normal potion healing.

### Stage 6 — Surgeries

Complete all 11 surgeries, including the preserved Adrenal Graft implementation.

Implement shared surgery offers, confirmed acquisition, once-per-run ownership, prices, persistence, and the actual mechanics of every surgery, including Retractable Talons’ bleed.

Review interactions with the skills, equipment, and afflictions already implemented.

### Stage 7 — Integration, community documentation, and delivery

Reconcile the implementation against the entire original specification and the 46-entry manifest.

Finish the Foundation guides, all four category tutorials, the standalone example, content catalog, installation instructions, manual gameplay checklist, migration notes, and rollback instructions.

Build both production plugins and the standalone example. Prepare the distributable packages and perform the authorized local deployment only when the game is closed and the required backups exist.

Follow the original publishing authorization rules.

## Rules for every stage

- Update relevant documentation as implementation changes. Stage 7 completes and checks the documentation; it is not the first time documentation gets written.
- Build affected projects and run meaningful nongame checks appropriate to the changes.
- Preserve all original restrictions on launching the game, gameplay automation, backups, deployment, and publishing.
- Do not deploy intermediate stages.
- Distinguish implemented code, successful builds, automated checks, and manually verified gameplay.
- Do not mark placeholders, definitions without mechanics, or unresolved blockers as complete.

## Execute now

Identify the earliest incomplete stage after inspecting the actual repository, then implement that stage.

If some work from later stages already exists, preserve it and account for it in the progress checklist.

At the end, report:
- What changed.
- What was built and checked.
- What remains unresolved or manually unverified.
- Whether the stage is complete.
- Which stage should come next.

Then stop. Do not automatically begin another stage.