# Manual Solo verification — 0.8.3

**Pending. Do not automate these checks.** The author reported a crash-free 0.8.2 session, but individual cases below remain pending. The 0.8.3 tag/normal-offer changes need a fresh check; see [current notes](COMMUNITY_UPDATE_0.8.3.md). Back up the complete native user-data/sidecar directory and matching DLL/configuration set first. Follow [installation and rollback](INSTALLATION.md). Use a disposable Solo run; multiplayer is unsupported. For normal-offer tests keep [DeveloperOffers] EnableForcedOffers=false. Deliberate testing overrides require that master switch plus the relevant Force option; migrated Force flags alone cannot force offers. F1 supports skills/injuries, ranks/severity, gold and temporary cheats. God mode prevents real damage tests: turn it off for damage-dependent entries. Debug grants are conveniences, not proof of normal acquisition. Equipment/surgeries must be bought through normal selection/cost paths.

For every row: check normal card/name/description and character/detail view where available, native eligibility/cost, actual effect and absence after removal/cure/unequip, plus the corresponding checkpoint. For every skill: ranks 0–4, short next-rank offer, owned-rank detail text and banner, no fifth rank, normal removal/reacquisition and restore. For every affliction: severity 1–3, suppression/reduction/cure and unchanged potion healing. Surgeries: genuine successful payment/completion and once-per-run ownership, no duplicate/replayed grants. Record the log, before/after resource values and pass/fail; no row below is already passed.

## Skills

<a id="sk01"></a>
- [ ] **SK01: Bulk Up** (11000): Rank 1: TC: 2x maximum health. Keep your health percentage. / Rank 2: TC: 3x maximum health. Keep your health percentage. / Rank 3: TC: 4x maximum health. Keep your health percentage. / Rank 4: TC: 5x maximum health. Keep your health percentage. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk02"></a>
- [ ] **SK02: Measured Violence** (11001): Rank 1: TC: +25% melee damage when attacking at 80%+ stamina. / Rank 2: TC: +50% melee damage when attacking at 80%+ stamina. / Rank 3: TC: +75% melee damage when attacking at 80%+ stamina. / Rank 4: TC: +100% melee damage when attacking at 80%+ stamina. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk03"></a>
- [ ] **SK03: Running on Fumes** (11002): Rank 1: TC: +35% melee damage when attacking below 25% stamina. / Rank 2: TC: +70% melee damage when attacking below 25% stamina. / Rank 3: TC: +105% melee damage when attacking below 25% stamina. / Rank 4: TC: +140% melee damage when attacking below 25% stamina. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk04"></a>
- [ ] **SK04: Catch Your Breath** (11003): Rank 1: TC: Stamina recovery delay: 1/2 while standing still. / Rank 2: TC: Stamina recovery delay: 1/3 while standing still. / Rank 3: TC: Stamina recovery delay: 1/4 while standing still. / Rank 4: TC: Stamina recovery delay: 1/5 while standing still. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk05"></a>
- [ ] **SK05: Expensive Taste** (11004): Rank 1: TC: +1% damage per 10 gold. Maximum +25%. / Rank 2: TC: +2% damage per 10 gold. Maximum +50%. / Rank 3: TC: +3% damage per 10 gold. Maximum +75%. / Rank 4: TC: +4% damage per 10 gold. Maximum +100%. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk06"></a>
- [ ] **SK06: Fresh Meat** (11005): Rank 1: TC: +30% damage to full-health enemies. / Rank 2: TC: +60% damage to full-health enemies. / Rank 3: TC: +90% damage to full-health enemies. / Rank 4: TC: +120% damage to full-health enemies. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk07"></a>
- [ ] **SK07: Pick Your Moment** (11006): Rank 1: TC: After 2s without attacking, your next attack deals +50% damage. / Rank 2: TC: After 2s without attacking, your next attack deals +100% damage. / Rank 3: TC: After 2s without attacking, your next attack deals +150% damage. / Rank 4: TC: After 2s without attacking, your next attack deals +200% damage. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk08"></a>
- [ ] **SK08: Mixed Martial Arts** (11007): Rank 1: TC: Alternate melee and thrown hits for +30% damage. Bonus lasts 6s. / Rank 2: TC: Alternate melee and thrown hits for +60% damage. Bonus lasts 6s. / Rank 3: TC: Alternate melee and thrown hits for +90% damage. Bonus lasts 6s. / Rank 4: TC: Alternate melee and thrown hits for +120% damage. Bonus lasts 6s. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk09"></a>
- [ ] **SK09: Payback Plan** (11008): Rank 1: TC: Store 50% of enemy damage for your next melee hit. Maximum 25% max HP; expires after 5s. / Rank 2: TC: Store 100% of enemy damage for your next melee hit. Maximum 25% max HP; expires after 5s. / Rank 3: TC: Store 150% of enemy damage for your next melee hit. Maximum 25% max HP; expires after 5s. / Rank 4: TC: Store 200% of enemy damage for your next melee hit. Maximum 25% max HP; expires after 5s. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk10"></a>
- [ ] **SK10: Waste Not** (11009): Rank 1: TC: Breaking a weapon restores 25% stamina. / Rank 2: TC: Breaking a weapon restores 50% stamina. / Rank 3: TC: Breaking a weapon restores 75% stamina. / Rank 4: TC: Breaking a weapon restores 100% stamina. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk11"></a>
- [ ] **SK11: Breathing Room** (11010): Rank 1: TC: Take 5% less damage per nearby enemy (3m). Maximum 20%. / Rank 2: TC: Take 10% less damage per nearby enemy (3m). Maximum 40%. / Rank 3: TC: Take 15% less damage per nearby enemy (3m). Maximum 60%. / Rank 4: TC: Take 20% less damage per nearby enemy (3m). Maximum 80%. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="sk12"></a>
- [ ] **SK12: Longshot** (11011): Rank 1: TC: 2x throw range and charge speed. / Rank 2: TC: 3x throw range and charge speed. / Rank 3: TC: 4x throw range and charge speed. / Rank 4: TC: 5x throw range and charge speed. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

## Equipment

<a id="eq01"></a>
- [ ] **EQ01: Runner’s Wraps** (2499): TC: Dodges use 25% less stamina while equipped. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq02"></a>
- [ ] **EQ02: Executioner’s Hood** (2488): TC: +35% melee damage to enemies below 30% health. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq03"></a>
- [ ] **EQ03: Lead-Lined Purse** (2489): TC: Take 1% less damage per 10 gold. Maximum 20%. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq04"></a>
- [ ] **EQ04: Borrower’s Scabbard** (2490): TC: A full-durability weapon's first hit each battle: +50% damage, +1 durability cost. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq05"></a>
- [ ] **EQ05: Butcher’s Apron** (2491): TC: Executions restore 3% health. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq06"></a>
- [ ] **EQ06: Last-Gasp Locket** (2492): TC: Once per battle: survive an enemy hit below 25% health to restore 50% stamina. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq07"></a>
- [ ] **EQ07: Mercenary’s Ledger** (2493): TC: Pay 5 gold at battle start for +20% damage that battle. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq08"></a>
- [ ] **EQ08: Pickpocket’s Mitts** (2494): TC: Your first backstab against each enemy grants 1 gold. Maximum 10 per battle. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq09"></a>
- [ ] **EQ09: Chalk Bag** (2495): TC: Aim a throw while still for 1.5s to deal +40% damage. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq10"></a>
- [ ] **EQ10: Salvager’s Hook** (2496): TC: Recover your thrown weapon to repair 1 durability. Once per weapon each battle. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq11"></a>
- [ ] **EQ11: Duelist’s Spurs** (2497): TC: +20% melee damage when only one enemy is within 4m. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="eq12"></a>
- [ ] **EQ12: Counterweight Sling** (2498): TC: Doubles throw range and charge speed. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

## Potion afflictions

<a id="af01"></a>
- [ ] **AF01: Leadfoot** (11010): TC: Movement speed reduced 10/20/30% at severity 1/2/3. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af02"></a>
- [ ] **AF02: Lead Belly** (11011): 1 skull(s): TC: Stamina starts recovering 0.25s later. / 2 skull(s): TC: Stamina starts recovering 0.5s later. / 3 skull(s): TC: Stamina starts recovering 0.75s later. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af03"></a>
- [ ] **AF03: Brittle Guard** (11012): 1 skull(s): TC: Every 4 shield blocks cost 1 extra durability. / 2 skull(s): TC: Every 3 shield blocks cost 1 extra durability. / 3 skull(s): TC: Every 2 shield blocks cost 1 extra durability. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af04"></a>
- [ ] **AF04: Shaky Resolve** (11013): 1 skull(s): TC: Enemy damage drains 5% stamina (1s cooldown). / 2 skull(s): TC: Enemy damage drains 10% stamina (1s cooldown). / 3 skull(s): TC: Enemy damage drains 15% stamina (1s cooldown). Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af05"></a>
- [ ] **AF05: Tin Ear** (11014): 1 skull(s): TC: Your actions generate 15% less crowd favor. / 2 skull(s): TC: Your actions generate 30% less crowd favor. / 3 skull(s): TC: Your actions generate 45% less crowd favor. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af06"></a>
- [ ] **AF06: Burning Lungs** (11015): 1 skull(s): TC: Stamina recovers 20% slower while moving. / 2 skull(s): TC: Stamina recovers 35% slower while moving. / 3 skull(s): TC: Stamina recovers 50% slower while moving. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af07"></a>
- [ ] **AF07: Heavy Landing** (11016): 1 skull(s): TC: Move 20% slower for 0.75s after dodging. / 2 skull(s): TC: Move 30% slower for 0.75s after dodging. / 3 skull(s): TC: Move 40% slower for 0.75s after dodging. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af08"></a>
- [ ] **AF08: Loose Change** (11017): 1 skull(s): TC: Enemy damage costs 1 gold (3s cooldown). / 2 skull(s): TC: Enemy damage costs 2 gold (3s cooldown). / 3 skull(s): TC: Enemy damage costs 3 gold (3s cooldown). Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af09"></a>
- [ ] **AF09: Cold Start** (11018): 1 skull(s): TC: Deal 10% less damage during the first ten seconds of each battle. / 2 skull(s): TC: Deal 20% less damage during the first ten seconds of each battle. / 3 skull(s): TC: Deal 30% less damage during the first ten seconds of each battle. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af10"></a>
- [ ] **AF10: Obsessive Focus** (11019): 1 skull(s): TC: After hitting an enemy, deal 15% less damage to others for 3s. / 2 skull(s): TC: After hitting an enemy, deal 25% less damage to others for 3s. / 3 skull(s): TC: After hitting an enemy, deal 35% less damage to others for 3s. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="af11"></a>
- [ ] **AF11: Overheating** (11020): 1 skull(s): TC: Repeated melee attacks cost +5% stamina (3 stacks). Rest 2s to reset. / 2 skull(s): TC: Repeated melee attacks cost +10% stamina (3 stacks). Rest 2s to reset. / 3 skull(s): TC: Repeated melee attacks cost +15% stamina (3 stacks). Rest 2s to reset. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

## Surgeries

<a id="su01"></a>
- [ ] **SU01: Adrenal Graft** (11020): TC: Stamina regenerates 25% faster. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su02"></a>
- [ ] **SU02: Anchor Heels** (11021): TC: Receive 75% less knockback. Dodges travel 20% less distance. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su03"></a>
- [ ] **SU03: Spring-Loaded Knees** (11022): TC: Dodges travel 30% farther but cost 20% more stamina. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su04"></a>
- [ ] **SU04: Dense Skeleton** (11023): TC: Take 15% less damage. Maximum stamina is reduced by 20%. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su05"></a>
- [ ] **SU05: Auxiliary Tendons** (11024): TC: Regular melee attacks cost 20% less stamina. -10% maximum health. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su06"></a>
- [ ] **SU06: Marrow Enrichment** (11025): TC: +30% healing. -15% maximum health. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su07"></a>
- [ ] **SU07: Pain Converter** (11026): TC: Enemy damage restores 1.5x its health percentage as stamina (max 25%). -10% maximum stamina. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su08"></a>
- [ ] **SU08: Emergency Clotting** (11027): TC: Once per battle: survive a 20%+ health hit to heal 25% of that loss over 4s. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su09"></a>
- [ ] **SU09: Counterweight Implant** (11028): TC: After stun or trip: next melee hit gains +25% damage and +75% knockback (5s). Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su10"></a>
- [ ] **SU10: Fury Pacemaker** (11029): TC: Fury gains instead restore 30% stamina (8s cooldown). Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

<a id="su11"></a>
- [ ] **SU11: Retractable Talons** (11030): TC: Unarmed hits bleed: 5 damage over 3s (refreshes). -10% weapon damage. Verify the benefit, tradeoff, threshold/cap/expiry and duplicate-callback behavior described in the category guide. Save/restore without a resource grant replay.

## Integration and regression matrix

- [ ] **Startup:** game closed during install; verify exactly two production DLLs, matching hashes and loader version. Manually start to menu, inspect fresh log for Foundation ready and pack load without missing methods, collision, interop or restore errors. Menu renders/responds/exits normally. Test Foundation alone on a backed-up fresh run, then optional independent examples plus pack; do not resume owned content with its owner absent. Missing/wrong Foundation must refuse the pack.
- [ ] **Configuration:** import existing eight booleans once, preserve an explicit destination false/true and F1 key. Fresh defaults have no forced offers or active cheats. Restart switches each category correctly; disabled owned content must refuse restore rather than silently erase it. Verify no duplicate registration after a restart.
- [ ] **Bulk Up:** partly damaged player, ranks 1–4 -> effective max 2/3/4/5x; proportional current HP once per real change. Lose it through a normal game mechanic, reacquire, then checkpoint/restore; normal removal must work without F1-specific bookkeeping. No tooltip/read/scene refresh may multiply HP or heal again. Native skills retain their original rank limits.
- [ ] **Independent content:** acquire Bulk Up and standalone Training Footwork plus several new skills and afflictions and two surgeries together. Save/exit/resume: independent ranks, severity, order and native histories, no purchases/healing/resource replay. Remove/cure each independently. Never copy a LegacyMarker for a second feature.
- [ ] **Paired checkpoints:** make two complete native-save/sidecar backups with different ranks/afflictions/surgeries/timers, then restore each whole matching set. On disposable copies, missing/mismatched sidecar, unknown schema and unavailable owned content must refuse restore and preserve files. Fresh run with the same seed and abandon/restart must not reuse old state. No complete arena/current-stamina snapshot is claimed.

### Longshot and Sling measurements

Use the same object, position, normalized charge, floor height and unobstructed flat ground. Record actual impact range and wall-clock full-charge time, not only a preview value. Check the preview agrees with the real throw. Normal held weapons, objects and limbs are included; bows, grapples/thrown enemies and independent generated projectiles use other paths.

| Longshot rank | Sling | Expected range / charge speed | Expected charge time | Manual result |
|---:|---|---:|---:|---|
| 0 | No | 1x | 1x | Pending |
| 0 | Yes | 2x | 1/2 | Pending |
| 1 | No | 2x | 1/2 | Pending |
| 1 | Yes | 4x | 1/4 | Pending |
| 2 | No | 3x | 1/3 | Pending |
| 2 | Yes | 6x | 1/6 | Pending |
| 3 | No | 4x | 1/4 | Pending |
| 3 | Yes | 8x | 1/8 | Pending |
| 4 | No | 5x | 1/5 | Pending |
| 4 | Yes | 10x | 1/10 | Pending |

- [ ] Remove/change Sling and ranks during charging: preserve normalized progress, no free completion or duplicate release. Re-equipping removes/restores only its own multiplier. Check highest-speed physics, terrain, native clamps, collisions and exceptions.
- [ ] **Wraps + Spring + Anchor:** cost `0.75 x 1.2 = 0.9`; distance `1.3 x 0.8 = 1.04`; normal running speed/dodge duration unchanged by these surgeries. Heavy Landing starts after dodge and does not shorten it.
- [ ] **HP/healing:** Bulk Up + Marrow + Tendons + Apron, partly damaged. Maximum factors multiply once, HP percentage transition once, native attainable cap respected. Apron only after a genuinely completed execution, Marrow affects native heals once. Death/downing and integer timed-heal rounding must not revive or generate repeated grants.
- [ ] **Gold:** spend/gain around 10-gold thresholds and caps for Taste/Purse, trigger Loose Change down to zero; immediate recalculation from actual money. Ledger insufficient funds, paid battle start, late equip, unequip/re-equip and restore: no second charge/free bonus. Mitts reward once per actual enemy/backstab, cap ten.
- [ ] **Mixed Martial Arts + Scabbard:** misses, blocks, successive hits, multi-target swings, melee -> thrown -> melee, six-second expiry; one reserved bonus, one opening durability extra, no generated-damage loop. Repair/drop/recover/re-equip must not rearm a spent physical weapon. Different weapons and new battles remain independent.
- [ ] **Pick Your Moment:** after two seconds idle, both direct and attributed area attacks receive the attempt-wide percentage once; a miss still consumes the attempt. Multi-target contacts don't add a second multiplier, and rank removal/reacquisition invalidates an old cached prime. Generated Talons ticks cannot inherit it.
- [ ] **Physical/event allowances:** normal durability break versus drop/despawn; genuine execution versus animation start; killed/despawned targets retain the original reward identity. Hook repairs own thrown weapon once per weapon/battle, below normal max. Same-process matching live objects versus newly reconstructed native arena objects follow the documented boundary.
- [ ] **Afflictions:** actual suppression, Pain Killers/Poultice/reduction/cure and bulk cure update pending effects. Lead Belly + Catch Your Breath + Adrenal + moving Burning Lungs must change delay/rate/grants separately. Obsessive Focus multi-target ordering and Overheating pre-payment stack cap/reset; no stale cached effect after same-frame cure/reacquisition. Tin Ear changes action contributions, not raw scores or pool/decay.
- [ ] **Fury Pacemaker:** multiple native fury-generating skills/effect sources; suppression throughout cooldown, at most one 30%-max-stamina conversion per eight seconds including nested callbacks, no recursion, correct battle/save continuity and ordinary expiry of existing fury.
- [ ] **Talons:** one unmitigated unarmed hit -> five extra HP across three seconds; repeat hits refresh one pair without immediate extra tick, multiple enemies/attackers stay independent. Weapon damage penalty only while owned; ticks cannot prime/consume direct-hit bonuses. Native mitigation, frame stalls, deaths/despawns, leaving battle, matching restore and vanished arena target cleanup.
- [ ] **Counterweight/Clot/Pain:** native recovery grace, five-second one-contact charge, 20% actual-loss Clot threshold/once per battle/four-second tail, Pain's actual-loss calculation and 25% cap. Exclude self/environment/friendly damage from enemy-loss triggers. Test Locket/Shaky/Pain ordering with normal resource clamps.
- [ ] **UI and native regression:** all card/detail/banner text and rank/severity state; pooled views after another tooltip, scene/menu transitions, native trainer/potion/equipment/surgery costs and selection, vanilla injury cure/skills/equipment, HUD and normal exit. F1 skill/potion rerolls have only earlier-version manual evidence; surgery reroll is still deferred. No original-icon import, reviewed translations or multiplayer verification is implied.

Record new screenshots manually only after these checks; retain the existing images as historical v0.7.3 context. Report exact startup errors and gameplay evidence before calling this version tested.

## 0.8.3 identification and normal offers

- [ ] Each custom card/detail/reveal description starts with TC exactly once; names and vanilla content remain untagged. Check the original four translated examples too.
- [ ] Keep the developer-offer master false, including with an older true Force flag: new eligible offers use normal pools. Deliberate testing requires master true plus the relevant Force flag and Solo authority. Existing stored offers/ownership are not rewritten.
