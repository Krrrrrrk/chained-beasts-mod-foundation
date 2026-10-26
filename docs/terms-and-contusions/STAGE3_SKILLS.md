# Stage 3: four-rank skills and normal throwing

Implemented locally and built against the existing BepInEx `6.0.0-be.791+b782da4` / `net6.0` bindings. **Not deployed or gameplay verified.** This stage implements twelve skills plus Counterweight Sling; remaining equipment, afflictions and surgeries retain their later stage boundaries.

## Content and formulas

`Skills/SkillDefinitions.cs` is the explicit identity table. `Skills/SkillMath.cs` contains the production calculations and rank text. `Skills/SkillsFeature.cs` connects definitions, presentation, events and modifiers. `Skills/SkillCombatState.cs` owns the timed/consumable state.

All skill ranks are clamped to 0–4. Rank zero contributes nothing. All twelve native SkillDefs have `maxLevel=4`; native trainer eligibility and costs remain in charge, and Foundation bounds only registered custom acquisitions. Vanilla rank caps are unchanged.

| Skill / ID | Rank-r behavior | Implementation boundary |
|---|---|---|
| Bulk Up / 11000 | Max HP ×(1+r); 2/3/4/5× at ranks 1–4 | Existing canonical ownership handler scales current HP by new effective max / old effective max, with attainable/death clamps. Restore assigns ownership without another adjustment |
| Measured Violence / 11001 | +25%r melee damage at ≥80% pre-payment stamina | Snapshot immediately before native ConsumeMeleeStamina; retain through contact rather than reading the depleted bar |
| Running on Fumes / 11002 | +35%r melee damage below 25% pre-payment stamina | Same snapshot; exactly 25% does not qualify |
| Catch Your Breath / 11003 | Stationary recovery delay / (1+r) | Advance only the existing recovery-delay timer while stationary; movement returns future clock steps to normal. Recovery rate is unchanged |
| Expensive Taste / 11004 | Bonus r × min(.01 × floor(max(gold,0)/10), .25) | Native Player.ModifyAttackDamage contribution reads actual current gold; covers player-attributed attack damage including its normal area path, without charge/resource recursion |
| Fresh Meat / 11005 | +30%r direct attack damage against full effective HP | Snapshot the hostile target before that hit; no bonus for an already wounded target |
| Pick Your Moment / 11006 | Next committed attack after 2s idle gets +50%r | Consume on the attempt, including a miss. The native attempt record carries readiness through delayed effects/projectiles. Removing the skill invalidates old readiness |
| Mixed Martial Arts / 11007 | Successful melee primes thrown weapon and vice versa, +30%r for 6s | One pending category, no self-stack; reserve for one contact, commit only on confirmed positive HP loss; failed/blocked hits release it |
| Payback Plan / 11008 | Store 50%r of actual enemy HP loss, cap 25% max HP; expires 5s after latest qualifying loss | Add the reservoir once, after outgoing percentage contributions, to a successful direct melee hit. Source attribution excludes internal/self/environment/friendly damage; overkill cannot inflate loss |
| Waste Not / 11009 | A normal durability break restores 25%r max stamina | Positive effective durability followed by raw break or native DestroyWeapon inside LoseDurability; nested callbacks deduplicated. Drops, default weapons and ordinary nonbreaking use grant nothing |
| Breathing Room / 11010 | Reduction min(.05r × nearby living hostile count, .20r), within 3m | Native enemy roster, alive/active and non-player-team filters; incoming damage contribution recomputed for each event |
| Longshot / 11011 | Range and charging speed ×(1+r) | Shared normal-throw pipeline below |

Independent percentage contributions multiply. Payback is one flat stored addition, then native target mitigation/blocking still runs. Fresh Meat is per hostile enemy before each hit. The other direct-hit rules also cover genuine HP-damaging contacts with destructible health objects; they are not silently limited to enemy types. Native `isMelee` distinguishes melee from other ranged contact, with explicitly mapped held throws retaining their category. Pick Your Moment is per committed attack, so multiple targets/projectiles belonging to that attempt share its earned attack bonus. Mixed Martial Arts and Payback have one reserved bonus each; only the first qualifying successful contact can spend it. Generated damage/area effects do not count as new direct contacts and cannot prime or spend these charges.

Skill timers use scaled gameplay seconds and preserve remaining duration across checkpoint/process clock changes. A genuine battle boundary resets transient attack state; repeated scene/state callbacks do not. Ranks, equipment and gold are read live where the specification requires them.

## Counterweight Sling and throw pipeline

`Equipment/CounterweightSlingFeature.cs` owns equipment ID **2498**, key `terms-and-contusions:equipment/counterweight-sling`, and initial price **45 gold**. Inspected native comparisons: Swift Sandals (RunningShoes) 40, Quilted Armor 45, Armor Plates 55, Leather Gloves (DoubleDurability) 75; Runner's Wraps is 40. Price 45 is an intentionally unbalanced starting value, stored as one constant. The definition reuses only Running Shoes sprite/model references, with its own identity/category/price. It joins the normal native shop pool and uses native equipment/history; no skill ranks or extra ammunition.

The sling independently contributes 2× range and charge speed while equipped. Longshot and sling factors combine through the shared modifier pipeline:

| Longshot rank | No sling | With sling | Charge time with sling, as fraction of normal |
|---:|---:|---:|---:|
| 0 | 1× | 2× | 1/2 |
| 1 | 2× | 4× | 1/4 |
| 2 | 3× | 6× | 1/6 |
| 3 | 4× | 8× | 1/8 |
| 4 | 5× | 10× | 1/10 |

`Runtime/NativeThrowing.cs` observes the real `NetworkAgent.ExecuteThrow` → launch RPC → delayed-launch coroutine → `Projectile.Launch` path. It carries an explicit attempt/provenance record through the native coroutine, including pooled projectile launch generation. It never labels arbitrary damage as the player's last throw.

For original launch speed v and elevation a, the transformed horizontal component is **M × v cos(a)**; the vertical component remains **v sin(a)**. Recompose force and angle with sqrt/atan2. Thus vertical flight time under the same gravity/height is unchanged and open-space horizontal displacement scales M. This also commutes with the inspected native uniform force contributions. Multiplying total launch speed by M would give a different result and is not used.

The same production transform is applied to the normal TelegrapherArc line. Its landing marker uses the line's actual force/angle/start height/weapon gravity, retaining the native half-distance local-space convention; it is not based on an unrelated default gravity. Normal throw targeting distance expands while aiming; melee and offhand-ability targeting are not expanded.

Charging is augmented at **one canonical native coroutine clock**: `NetworkAgent._RunWindupLoop_d__443.MoveNext`. Its existing wait timer and held weapon charge timer receive the extra elapsed step appropriate to their current phase. Native progress remains in charge; no duration getter or charge-scale getter also applies the multiplier. Fixed post-release launch delays remain native. Frame boundaries between windup and charging introduce ordinary frame quantization, so stopwatch checks need roughly a frame or two of tolerance, especially at 10× speed.

Changing equipment/rank during charging changes **future clock steps**, without rebasing an already earned charge or creating another throw. Range is captured from the current sources at release and stays with that launched object; unequipping later does not alter an in-flight projectile. Invalidated/stale weapon coroutines are not advanced.

Ordinary held weapons and throwable objects using ThrowWeapon follow this path, including normal thrown props/limbs. Native collisions, gravity, damage scale, stamina costs, durability/model/history and other object behavior are preserved. Obstacles, native random launch deviations, physics limits and special projectile behavior still require the manual checks below. Fired bow ammunition, offhand ability throws, thrown enemies/grapples and independently spawned traps/generated effects are separate native paths; they do not receive the normal-throw range/charge bonus. Direct bow hits still participate in applicable direct-attack skills. No separate artillery/ammunition system is added.

## Presentation and configuration

Normal name/description terms use Foundation's owned I2 API. Current and next-rank descriptions are selected from actual Solo ownership, with targeted trainer Refresh/SetSkill, character skill-info and skill-tooltip integration. Rank 4 shows maximum rank. The previously confirmed Bulk Up backing-row acquisition-banner fix remains; no banner scanner or broad native UI detour is reintroduced.

The old twelve-language names/text aliases for the four existing examples remain; Bulk Up's old description aliases summarize cumulative 2/3/4/5× HP in all twelve supplied languages. New skill/sling text and dynamic rank panels currently use explicit **English fallback**, including when another game language is selected. Translations/layout are not claimed manually verified. 0.8.2 replaces long combined current/next paragraphs with short next-rank offers and actual owned-rank detail text at the user's request. Check actual font wrapping and the name/description in each native card/reveal; character-count checks alone do not prove fit.

After eventual final deployment, pack config is `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`:

- Existing `BulkUp.EnableBulkUp` and all existing enable/force choices are retained.
- `Skills.EnableMeasuredViolence`, `EnableRunningOnFumes`, `EnableCatchYourBreath`, `EnableExpensiveTaste`, `EnableFreshMeat`, `EnablePickYourMoment`, `EnableMixedMartialArts`, `EnablePaybackPlan`, `EnableWasteNot`, `EnableBreathingRoom`, `EnableLongshot` are startup content switches, default true.
- `CounterweightSling.EnableCounterweightSling` defaults true.
- No new forced offers or cheats default on. Existing deliberate force choices migrate unchanged. F1's generic skill browser sees the new names and four-rank caps; normal shop rerolls can help locate the sling.

Restart to apply switches. Disabling/removing an owned entry blocks checkpoint restoration until the compatible owner is enabled, rather than deleting ownership.

## Persistence and rollback

Existing schema-1 rank/order records store all twelve skills independently. Original Bulk Up rank-one imports stay rank one/2× and do not heal on migration. Native surgery/equipment histories remain authoritative. `terms-and-contusions:skills/combat-v1` stores attack age, the mixed category/remaining duration, and Payback amount/remaining duration, with its own validated schema. Restoration assigns this state and does not replay resources or attempt an attack.

Stage 3 adds a prerequisite safeguard to the shared system: feature-state validation runs before the native restore starts, and failed feature capture blocks the corresponding native SaveData write. Unresolved native HP receipts/charge reservations also block a new custom checkpoint instead of making a consumed bonus available again. A late receipt can still resolve normally; a timeout logs that it remains reserved and does not invent success or release spendable damage. Run/battle reset clears those transient records.

Back up/restore native saves and `ModFoundation/checkpoints` together. See [the protocol](../foundation/PERSISTENCE.md). Stage 3's pre-change checkpoint is `backups/expansion-stage3-20261025-165045`, outside loader scan paths, with source/docs/scripts/tests/examples, built Stage 2 artifacts, deployed v0.7.3, configuration, user data and hashes. No installed rollback is needed now: this stage is build-only. A downgrade of a future Stage 3 content-bearing save requires the matching older complete user-data backup, not just an older DLL.

## Meaningful checks and manual handoff

Run `pwsh -NoProfile -File .\scripts\test-stage3.ps1`. It builds Foundation, pack and standalone example; retains prior configuration/persistence/metadata checks; links the actual production skill math/state/throw calculations into a nongame runner; checks all rank/stacking/inverse-time cases, thresholds/caps, health transitions, multi-source damage, reservation/idempotence, timer round trips, break filtering, native targets and all new ID vacancies. No game is initialized or native gameplay handler invoked.

At final authorized deployment, manually check:

1. Every skill appears through the normal trainer, shows short next-rank offer and owned-rank detail text, and rank 4 maximum, and never grants a fifth rank. Vanilla caps/costs/ownership remain unchanged. Confirm Bulk Up's acquisition banner at several ranks.
2. Bulk Up 60/100 → 120/200 → 180/300 → 240/400 → 300/500; normal rank removal reverses proportionally, attainable limits/death are respected, and checkpoint restore never grants the adjustment again.
3. Measured/Fumes around 80% and 25% **before payment**, with ordinary and special melee, flail and multi-target attacks. Fresh Meat applies only before the first damaging hit at full effective health; gold changes immediately update Expensive Taste.
4. Catch Your Breath changes stationary delay, not recovery rate; movement during the wait changes future progress. Check with native modifiers and Adrenal Graft.
5. Wait two seconds, miss, then attack again for Pick Your Moment. Alternate melee/thrown hits for Mixed Martial Arts; test expiration, blocked hits and multiple targets. Payback must use actual enemy HP loss, keep its fixed cap, reject self/environment/friendly/zero damage, spend once and expire/restore correctly.
6. Break a real weapon for Waste Not; ordinary durability loss, drops, duplicate callbacks and default unarmed attacks must not restore stamina. Count living hostile enemies at the three-metre boundary for Breathing Room.
7. Measure Longshot ranks 0–4 with and without sling using comparable objects, launch height and **equal normalized charge** on open ground. Compare actual landing, arc, landing marker and targeting; test weapon gravity overrides and highest-speed physics behavior. Independently measure inverse charging times with frame tolerance; change ranks/equipment while charging and confirm one release only.
8. Remove/reacquire ranks and equipment, change scenes, restore the same and older matching checkpoints, and start a fresh run. Timed state must neither replay resources nor carry into another battle/run. Confirm no recurring `awaits native HP confirmation` or capture/restore errors.

All runtime hooks, damage attribution, HUD, actual throws, targeting and charging remain **awaiting manual gameplay verification**. Adrenal Graft's old completion heuristic and surgery-reroll issue remain Stage 6/deferred work. Next is Stage 4's remaining equipment; no Stage 4 mechanics were implemented here.
