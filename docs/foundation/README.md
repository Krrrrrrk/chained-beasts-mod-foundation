# Make your own mod with Chained Beasts Mod Foundation

Foundation supplies reusable registration, checkpoint state, modifiers and lifecycle integration. Separate plugins own definitions and gameplay rules. **0.8.0 is locally built, runtime unverified and unpublished.** Read [installation and rollback](../INSTALLATION.md) before migration. See the [separate mod walkthrough](SEPARATE_MOD.md), [mechanics](MECHANICS.md) and [localization/visuals](LOCALIZATION_VISUALS.md).

## Products and prerequisites

| Product | Assembly | BepInEx GUID | Built version |
|---|---|---|---|
| Foundation | `ChainedBeasts.ModFoundation.dll` | `local.chainedbeasts.modfoundation` | `0.8.0`, unreleased |
| Terms & Contusions | `TermsAndContusions.dll` | `local.chainedbeasts.termsandcontusions` | `0.8.0`, unreleased |
| Optional teaching example | `StandaloneContentExample.dll` | `example.chainedbeasts.standalonecontent` | `0.8.0`, not shipped with pack |

Use Windows x64, PowerShell 7 (`pwsh`), a .NET SDK/targeting pack capable of `net6.0`, the inspected game/Unity `6000.3.21f1`, and BepInEx `6.0.0-be.791+b782da4`. References come from the existing loader's `BepInEx/interop`; never substitute inspection stubs or redistribute those DLLs. `-GameRoot` or `CHAINED_BEASTS_GAME_DIR` selects another installation. Installed native Newtonsoft.Json is used only inside the checkpoint adapter, with a separate reference alias.

```powershell
pwsh -NoProfile -File .\scripts\build-plugins.ps1
pwsh -NoProfile -File .\scripts\test-stage7.ps1
dotnet build .\examples\StandaloneContentExample\StandaloneContentExample.csproj -c Release
```

The build command supports `-FoundationOnly`. The check command builds both production projects and the independent example, checks configuration/metadata, then exercises production pure logic without initializing Unity. These commands never deploy or launch the game. Deployment requires explicit -Deploy and makes a matching complete checkpoint; default build/check invocations cannot install.

The pack and example declare a hard dependency on Foundation `>=0.8.0 <0.9.0`. The installed BepInEx API interprets a bare version as exact, so the range is intentional. Missing/incompatible Foundation blocks the dependent plugin. A failed shared-core install leaves `FoundationServices.IsReady=false`; dependent plugins explicitly refuse to initialize.

## Public extension points

| API / source area | Responsibility |
|---|---|
| `ContentRegistry`, `ContentDescriptor` (`Content/`) | Stable owner/key/category/ID/rank bindings; installed-native and cross-registration collisions; disabled identity reservations |
| `FoundationServices.RegisterLibraryCallbacks` (`Runtime/`) | Native library factory and post-init verification callbacks for skills, equipment and afflictions; duplicate/live foreign-definition checks belong in the factory |
| `FoundationText.Register` | Owned I2 terms, optional translations keyed by native language names, explicit English fallback, direct term/pooled-label integration |
| `FoundationServices.RegisterModifier`, `StatChannel` (`Stats/`) | Fresh-baseline HP/movement/dodge/recovery plus attack damage, recovery-delay clock, normal throw range/charge-speed channels; removal/disable cannot leave a cached multiplier |
| `OwnershipChanged` | Actual canonical skill/injury before/after mutation, including normal removal; excludes no-ops, nested duplicate delivery, failures, and restoration assignment |
| `RunReset`, `CheckpointRestored`, `BattleStarted`, `EquipmentChanged` | Existing implemented lifecycle boundaries; battle key is run + completed-level scope, equipment event requires actual change |
| `FoundationServices.PlayerState(player)` (`State/`) | Namespaced per-player feature JSON included in exact checkpoint snapshots |
| `CheckpointStore`, `StateProjection`, `RunStateSession` | Pure production persistence/migration/availability/lifecycle logic used by the game adapter and nongame tests |
| `FeaturePatcher` | Exact-signature resolution, caller-owned Harmony patches, known-unsafe-target refusal and distinct-wrapper native-entry collision guard; read [native hook safety](NATIVE_HOOK_SAFETY.md) before adding detours |
| `LegacyConfigurationMigration` (`Configuration/`) | One-time defaults from old BepInEx config, preserving explicit destination settings |
| `SessionAccess.CanUseDeveloperTools` | Real Solo + host + exactly one local client/player; false outside that authority |
| `DeveloperToolsEvents.SkillRemoved` | Retained compatibility notification for this menu only; use canonical `OwnershipChanged` for gameplay |

Stage 3 adds `CombatServices.AttackCommitted`, `PreparingDirectHit`, `DirectHitResolved`, `EnemyHpLost`, `WeaponBroken` and `IncomingDamage`, with scoped attempts, pre-cost stamina, actual HP receipts and explicit launch provenance. `CheckpointCapturing` and fail-closed `CheckpointValidating` let content serialize/validate its feature schema. See [native evidence](STAGE3_EVIDENCE.md). No game damage/grant handler is executed by the nongame checks.

Foundation references no TermsAndContusions assembly/content type and creates no pack definitions by itself. Surgeries have no native library registry; shared surgery offers and confirmed completion are implemented in Stage 6. Stages 3–6 provide direct-attack/normal-break/normal-throw provenance, confirmed executions, physical weapon/enemy allowances and explicit generated native damage. The pack supplies Talons bleed state through these public services.

Start with the [buildable standalone skill](../../examples/StandaloneContentExample/README.md), [current content guide](../ADDING_CONTENT.md), [persistence protocol and rollback policy](PERSISTENCE.md), [architecture](ARCHITECTURE.md), and [native evidence](STAGE2_EVIDENCE.md). Do not copy legacy reserved-slot bridges.

## Configuration, runtime status and next work

Foundation retains `BepInEx/config/local.chainedbeasts.modfoundation.cfg` for diagnostics and developer tools; content booleans migrate to the pack's own configuration. See [configuration migration](CONFIGURATION_MIGRATION.md). God mode and infinite stamina begin false and clear on run reset/leaving the allowed Solo context.

Expected new startup markers below are source expectations, **not observed runtime evidence**:

```text
[Foundation] checkpoint bridge installed; sidecars publish only after successful native platform writes
[Terms & Contusions] content pack loaded; shared identities and checkpoint persistence enabled
```

Manual verification is still required for chainloader/dependency behavior, real IL2CPP hooks, serialization/write completion, HUD changes, removal/reacquisition, legacy import, older-checkpoint restore and missing-content refusal. Keep the matching native save and sidecars together; do not resume a run with an absent/disabled owning plugin.

Stages 3–5 implement skills/throwing, equipment and afflictions; runtime verification remains manual. Stage 4 adds `AttackRecord.Weapon`, stable pre-hit `DirectHit.TargetId`/`IsBackstab`, flat-aware `MultiplyDamage`, confirmed `ExecutionConfirmed`, `WeaponRecovered`, queued `ConsumeOpeningDurability`, and `GameplayTick` (consumers gate mutations outside Solo). See [Stage 4 API evidence](STAGE4_EVIDENCE.md).

Stage 5 adds actual stamina-spend/recovery, melee-payment, shield-feedback, dodge-completion and player crowd-contribution events, target/action context and a separate regeneration-grant channel covering normal/haste branches. The two-argument stat-context constructor and existing channel values remain compatible. Native effective severity and timed/counter mechanics remain in the pack. See [Stage 5 API evidence](STAGE5_EVIDENCE.md) and [affliction manual checks](../terms-and-contusions/STAGE5_AFFLICTIONS.md). Stage 6 adds shared surgery offers/confirmed completion, maximum-stamina/dodge-distance/healing channels, special-payment and unarmed provenance, generated native damage and validated surgery timers. See [Stage 6 evidence](STAGE6_EVIDENCE.md) and [surgery mechanics/manual checks](../terms-and-contusions/STAGE6_SURGERIES.md). Stage 7 supplies [category tutorials](SEPARATE_MOD.md), [installation/packages](../INSTALLATION.md) and [local delivery status](../STAGE7_HANDOFF.md). See [progress](../EXPANSION_PROGRESS.md). Original code/docs are MIT; game and loader binaries are not distributed.
