# Mechanics, lifecycle and safe composition

The implemented API lives in `src/ChainedBeasts.ModFoundation/{Content,Runtime,Stats,State,Logic}`. The pack provides balance/mechanic decisions; Foundation handles native provenance/authority and generic state. See [architecture](ARCHITECTURE.md), [native evidence](STAGE6_EVIDENCE.md), [complete catalog](../terms-and-contusions/CATALOG.md) and [persistence](PERSISTENCE.md). Public surfaces are specific to the inspected build, not a generic Unity mod SDK.

## Fresh stats versus one-time events

`RegisterModifier(contentKey,StatChannel,Func<NativeStatContext,double>)` composes independent percentage factors from a fresh otherwise-normal native result in stable-key order. Disabled/unowned contributions are neutral. Channels are MaximumHealth, MovementSpeed, DodgeStaminaCost, StaminaRecoveryRate, AttackDamage, RecoveryDelayClockRate, ThrowRange, ThrowChargeSpeed, RegenerationAmount, MaximumStamina, DodgeDistance, IncomingKnockback and HealingAmount. Existing numeric values/context constructors are preserved. Don't store a previously modified maximum as the next baseline.

All twelve pack skills clamp rank to 0–4. Bulk Up is `1+r`, not repeated doubling per rank. `OwnershipChanged` reports actual canonical AddSkill/RemoveSkill and AddInjury/RemoveInjury transitions with before/after rank, HP and old max, excluding no-op/nested/failure/restore paths. The pack uses `HealthTransition.Scale` once for proportional Bulk Up HP. The surgery completion adapter likewise scales HP/stamina only when confirmed ownership changes the maximum. Native attainable caps and no-revival rules remain in force. Loading ownership never invokes an acquisition method.

`Catch Your Breath` accelerates only the eligible recovery-delay clock while stationary; it doesn't change recovery rate. Lead Belly adds one owned/reconciled delay tail and suppression/cure removes only that tail. Burning Lungs scales passive regeneration amounts, including native haste branches. One-time grants use `CombatServices.RestoreStaminaGrant`, preserving owner authority and normal clamp while bypassing passive-regeneration factors even in nested contexts. Marrow scales evidenced healing amounts before native health clamps. Integer HP/heal installments can round differently from one combined amount; verify small-value cases manually.

Percentage factors multiply: Wraps x Spring is `0.75 x 1.2 = 0.9` dodge cost; Anchor x Spring is `0.8 x 1.3 = 1.04` distance; Dense x Pain gives `0.8 x 0.9 = 0.72` max stamina. Bulk rank two plus Tendons/Marrow produces `3 x 0.9 x 0.85` maximum HP. No UI refresh may heal, charge currency or pay a benefit.

## Throw matrix and concrete boundaries

| Rank | Without Sling: range/speed; time | With Sling: range/speed; time |
|---:|---|---|
| 0 | 1x; 1 | 2x; 1/2 |
| 1 | 2x; 1/2 | 4x; 1/4 |
| 2 | 3x; 1/3 | 6x; 1/6 |
| 3 | 4x; 1/4 | 8x; 1/8 |
| 4 | 5x; 1/5 | 10x; 1/10 |

Actual normal ThrowWeapon provenance links input/RPC, delay coroutine, ProjectileLaunch, preview and weapon transfer. Horizontal ballistic range is modified at the native launch/preview path while native vertical motion/collisions remain; charge acceleration happens once in the canonical clock. The native coroutine bindings are build-specific. Equal normalized charge is required when comparing range. Changing rank/equipment doesn't reset charging or manufacture another launch. Held weapons/objects/limbs using normal throws are supported; bows, offhand abilities, grapples/thrown enemies and independent traps/generated projectiles use other paths. High-speed physics/preview accuracy remains manual verification, not a calculated-distance guarantee.

## Attack and damage provenance

`AttackCommitted` exposes an independent `AttackRecord.Id`, actor, pre-payment stamina fraction, kind, explicit unarmed classification and physical weapon token. Commit consumes attempt-based primes such as Pick Your Moment even on a miss. Pick's percentage is an attempt-wide AttackDamage contribution, including attributed area attacks; the direct-contact path does not add it again. Native melee payment exposes whether it is special; Tendons discounts only regular payment, while native/Overheating payment remains composed.

`PreparingDirectHit` carries actor/target IDs, captured pre-hit HP/effective max/backstab, attack/kind and mutable damage. `DirectHitResolved` supplies actual positive HP loss from the authoritative Execute-stage native receipt; blocked/failed hits resolve zero. Charge reservations stay bound to one contact until confirmation, including delayed receipts, and prevent another target consuming the same bonus. A timeout logs and retains unresolved reservations; it does not free a second bonus. Checkpoint capture refuses unsettled state.

Use `DirectHit.MultiplyDamage` rather than blindly multiplying its total when adding a percentage after Payback's flat reservoir; flat damage remains a single addition after percentage contributions. Mixed Martial Arts primes the opposite direct category after success, with six-second bounded charges. Pick consumes the committed attempt; Scabbard/Payback/Counterweight consume successful contacts. Multi-target hit callbacks belong to the same action but remain individual contacts; a reserved single-use bonus cannot be duplicated across them. Obsessive Focus captures previous focus per action and deterministically chooses the next successful target by stable captured native order.

`EnemyHpLost` excludes internal, self, environmental and friendly sources and uses actual loss rather than attempted damage or overkill. Incoming reduction happens before that receipt. Locket, Shaky and Pain have deliberate subscriber ordering and normal resource clamps. Executions require the actual scoped execution damage receipt and a dead enemy, not an animation-start callback. Normal durability transitions establish WeaponBroken; drops/despawns alone don't.

Generated effects use `DealGeneratedDamage` with actual attacker provenance, a directionless source and explicit generated scope. Talons ticks cannot become a new direct unarmed attack or consume MMA/Payback/Scabbard. The HP-dispatch queue includes generated/unrelated native RPCs so their receipts cannot settle a pending direct bonus/execution on the same attacker/target. Fury conversion reserves its cooldown before granting stamina; positive Rage writes are suppressed during cooldown while negative timer decay/other statuses remain native.

## State scopes and extension points

`RunReset`, `BattleStarted`, `EquipmentChanged`, `CheckpointRestored` and `GameplayTick` are actual public events. Battle identity is run + completed-level scope, deduplicated against UI/scene updates. Real equipment switching doesn't reset per-battle ledgers. Gameplay subscribers gate their own mutation through the real Solo/authority predicate; one blanket host-only condition is insufficient for owner-written stamina versus server HP/money.

Physical weapon GUIDs are independent tokens transferred through observed held/drop/throw/projectile/landing/embed/recovery representations. Runtime pointers/network IDs are transient aliases, not persistent identity. Scabbard's spent opener and Hook's repair survive drop/recovery/repair/re-equip within that battle; Mitts uses pre-despawn enemy tokens and a ten-gold cap. A native-created new weapon/enemy on resume is a new arena spawn: native checkpoints do not serialize the full arena or current stamina/recovery timers.

For additional state, use `FoundationServices.PlayerState(data).SetFeature(yourVersionedKey,value)` during `CheckpointCapturing`; validate every player's `GetFeature<T>` during `CheckpointValidating`; restore remaining durations in `CheckpointRestored` without any live acquisition/resource call. The pack's SkillCombatState, EquipmentBattleState, AfflictionBattleState and SurgeryBattleState are complete concrete implementations, not independent global "latest state" files. Capture/validation is fail-closed, whereas advisory event failures are logged. Unknown schemas, missing content and older backups follow the [exact checkpoint protocol](PERSISTENCE.md).

New mechanics outside these channels/events need installed-interop/native inspection: weapon-specific attack families, custom original assets, networking/authority, offhand/projectile systems and arena reconstruction aren't invented by adding a descriptor. See the [manual matrix](../MANUAL_TESTS.md) for remaining native evidence.
