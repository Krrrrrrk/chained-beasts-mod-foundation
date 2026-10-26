# Make your own mod with Chained Beasts Mod Foundation

**Just want to play Terms & Contusions?** Use the [player quick install](../../README.md#quick-install). This page is for building source and making mods.

Foundation supplies reusable registration, checkpoint state, modifiers and lifecycle integration. Separate plugins own their definitions and gameplay rules. The current public release is [**0.8.3**](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3). Foundation can be installed without Terms & Contusions.

Start with the setup below, then follow the [separate-mod walkthrough](SEPARATE_MOD.md). The author reported a crash-free 0.8.2 playtest; the 0.8.3 presentation/offer changes and individual mechanics still need manual checks. See [release notes](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3).

## Products and prerequisites

| Product | Assembly | BepInEx GUID | Built version |
|---|---|---|---|
| Foundation | `ChainedBeasts.ModFoundation.dll` | `local.chainedbeasts.modfoundation` | `0.8.3`, published |
| Terms & Contusions | `TermsAndContusions.dll` | `local.chainedbeasts.termsandcontusions` | `0.8.3`, published |
| Optional teaching example | `StandaloneContentExample.dll` | `example.chainedbeasts.standalonecontent` | `0.8.0`, not shipped with pack |

Install these development prerequisites:

- **Git** to clone the source.
- **PowerShell 7** (`pwsh`) to run the repository scripts.
- A **.NET SDK/targeting pack supporting `net6.0`**.
- The inspected Windows x64 game setup: **Chained Beasts 1.0.1**, Unity **6000.3.21f1**, and **BepInEx 6.0.0-be.791+b782da4 Unity IL2CPP x64**.

Complete the [BepInEx first-time setup](../INSTALLATION.md#2-install-bepinex-once) and one normal game launch before building, then close the game. References come from the loader's generated `BepInEx/interop` directory; never substitute Cpp2IL inspection stubs or redistribute those DLLs. Installed native Newtonsoft.Json is used only inside the checkpoint adapter, with a separate reference alias.

## Clone and build

```powershell
git clone https://github.com/Krrrrrrk/chained-beasts-mod-foundation.git
cd chained-beasts-mod-foundation
```

The scripts default to `C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts`. For another installation, set this variable to the actual game folder **before building**:

```powershell
$env:CHAINED_BEASTS_GAME_DIR = 'D:\Games\Chained Beasts'
```

From the repository folder, build the project:

```powershell
pwsh -NoProfile -File .\scripts\build-plugins.ps1
dotnet build .\examples\StandaloneContentExample\StandaloneContentExample.csproj -c Release
```

Create local release ZIPs after a successful build:

```powershell
pwsh -NoProfile -File .\scripts\package-plugins.ps1 -SkipBuild
```

Packages are written to `artifacts/packages/0.8.3`. For deployment and paired rollback, read the [advanced script section of the installation guide](../INSTALLATION.md#building-and-scripted-installation) before using the release helpers.

The build command supports `-FoundationOnly`. Build commands never deploy or launch the game. Deployment requires explicit -Deploy and makes a matching complete checkpoint; default build invocations cannot install.

The current pack and example declare a hard dependency on Foundation `>=0.8.0 <0.9.0`. Use the matching **0.8.3** production DLLs for the current release; the declaration alone does not establish that every older pairing is tested. The installed BepInEx API interprets a bare version as exact, so the range is intentional. Missing/incompatible Foundation blocks the dependent plugin. A failed shared-core install leaves `FoundationServices.IsReady=false`; dependent plugins explicitly refuse to initialize.

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
| `FeaturePatcher` | Exact-signature resolution, caller-owned Harmony patches, known-unsafe-target refusal and distinct-wrapper native-entry collision guard; read [native hook safety](MECHANICS.md#native-hook-safety) before adding detours |
| `LegacyConfigurationMigration` (`Configuration/`) | One-time defaults from old BepInEx config, preserving explicit destination settings |
| `SessionAccess.CanUseDeveloperTools` | Real Solo + host + exactly one local client/player; false outside that authority |
| `DeveloperToolsEvents.SkillRemoved` | Retained compatibility notification for this menu only; use canonical `OwnershipChanged` for gameplay |

Combat services expose `CombatServices.AttackCommitted`, `PreparingDirectHit`, `DirectHitResolved`, `EnemyHpLost`, `WeaponBroken` and `IncomingDamage`, with scoped attempts, pre-cost stamina, actual HP receipts and explicit launch provenance. `CheckpointCapturing` and fail-closed `CheckpointValidating` let content serialize/validate its feature schema. See [native hook safety](MECHANICS.md#native-hook-safety). No game damage/grant handler is executed by the nongame checks.

Foundation references no TermsAndContusions assembly/content type and creates no pack definitions by itself. Surgeries have no native library registry; shared surgery offers and confirmed completion are provided by SurgeryServices. The runtime adapters provide direct-attack/normal-break/normal-throw provenance, confirmed executions, physical weapon/enemy allowances and explicit generated native damage. The pack supplies Talons bleed state through these public services.

Start with the [buildable standalone skill](../../examples/StandaloneContentExample/README.md), [current content guide](../ADDING_CONTENT.md), [persistence protocol and rollback policy](PERSISTENCE.md), [architecture](ARCHITECTURE.md), and [native hook safety](MECHANICS.md#native-hook-safety). For runtime hooks and presentation, see [mechanics and events](MECHANICS.md) and [localization and visuals](LOCALIZATION_VISUALS.md). Do not copy legacy reserved-slot bridges.

## Configuration and compatibility

Foundation uses `BepInEx/config/local.chainedbeasts.modfoundation.cfg`; the pack owns its separate configuration. See [configuration and migration](CONFIGURATION_MIGRATION.md). Developer tools require a real Solo session, one local host/player, and explicit configuration. Temporary cheats clear when that context ends.

Keep native saves and sidecars together. Do not resume a run with a missing or disabled owning plugin. New mods need manual checks of their callbacks, resource/HUD changes, removal and checkpoint restoration; publication is not a guarantee that every interaction has been verified.

The shared [mechanics/events guide](MECHANICS.md) covers direct-hit receipts, physical weapon identity, stamina/shield/dodge events, surgery completion and safe stat composition. Use the [category tutorials](SEPARATE_MOD.md) and [installation guide](../INSTALLATION.md) to build and install your own example.
