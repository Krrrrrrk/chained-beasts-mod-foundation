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
