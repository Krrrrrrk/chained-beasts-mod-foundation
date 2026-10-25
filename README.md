# Chained Beasts Mod Foundation

An unofficial BepInEx 6 mod and source-level custom-content starter kit for **Chained Beasts**.

The current build adds four pieces of custom content and an optional solo developer menu. Each example follows the game's normal offer, purchase, acquisition, ownership, and run-state paths rather than editing shipped assets or save files directly.

## Included content

| Content | Type | Effect |
|---|---|---|
| **Bulk Up** | Skill | Doubles maximum and current HP when acquired. Health percentage is preserved. |
| **Runner's Wraps** | Equipment | Dodges consume 25% less stamina while equipped. Costs 40 gold. |
| **Leadfoot** | Potion affliction | Reduces movement speed by 10%, 20%, or 30% at one, two, or three skulls. The generated potion's normal healing is preserved. |
| **Adrenal Graft** | Surgery | Increases stamina recovery speed by 25% for the rest of the run. Currently free. |

> [!IMPORTANT]
> These additions are technical examples of what custom content can do, not balanced gameplay content. No attempt has been made to tune them for difficulty or progression; individual examples may be extremely overpowered, underpowered, or otherwise unsuitable for a normal run.

Custom content retains the surrounding native behavior: offer eligibility, normal UI interaction, costs, acquisition statistics, ownership checks, and checkpoint bookkeeping. The effects are applied through narrow runtime hooks and do not replace unrelated game systems.

The examples currently reuse sprites, models, and other visual references that are already loaded by Chained Beasts. This project does **not** yet demonstrate importing or assigning brand-new icons, and it does not redistribute the game's assets.

## Screenshots

| Bulk Up trainer offer | Runner's Wraps in the shop |
|---|---|
| ![Bulk Up shown as a trainer skill](docs/images/bulk-up-offer.png) | ![Runner's Wraps shown in an equipment shop](docs/images/runners-wraps-shop.png) |

| Leadfoot potion | Adrenal Graft surgery |
|---|---|
| ![Leadfoot shown as a potion affliction](docs/images/leadfoot-potion.png) | ![Adrenal Graft shown at the surgeon](docs/images/adrenal-graft-surgery.png) |

![The optional solo developer menu open during a test run](docs/images/developer-menu.png)

## Solo developer menu

Press **F1** during a solo lobby or run to open the optional testing panel.

It provides:

- God mode
- Infinite stamina
- Heal to full
- Gold adjustments
- Add or remove one rank of an offerable skill
- Add or remove one level of an injury/debuff
- Reroll the offers on the currently open skill, potion, or shop screen
- Experimental surgery refresh (currently animates but can keep every offer unchanged)

The reroll button is context-sensitive: open the offer screen first, then press F1 and choose **Reroll current offers**. It clears any unconfirmed selections on that screen, replaces only the visible offer set, and leaves other future rooms alone.

Surgery rerolls are currently unreliable: the panel refresh animation plays, but the user's test kept all the same options. Treat this as a known issue rather than a working reroll feature. Further surgery-reroll work is deferred. The forced Adrenal Graft option stays pinned when its testing override is enabled.

The run-data tools target player 1 and require the local client to be the host. God mode and infinite stamina reset when the game closes. Health, gold, skills, injuries, and rerolled offers are real run data and can be written into the active checkpoint, so use the menu on disposable test runs.

## Compatibility

The current build targets this game/loader baseline. The user confirmed version 0.7.3 fixes Bulk Up's acquisition banner, and potion and skill rerolls work in solo play. Surgery rerolls refresh the panel but can leave all offers unchanged:

| Component | Tested version |
|---|---|
| Chained Beasts | `1.0.1` — Steam App ID `3569420` |
| Unity | `6000.3.21f1` |
| BepInEx | `6.0.0-be.791+b782da4`, Unity IL2CPP, Windows x64 |
| Plugin | `ChainedBeasts.ModFoundation 0.7.3` |
| .NET target | `net6.0` |
| `GameAssembly.dll` SHA-256 | `72f2892607b0772470caae5db766dc07aaf636c3edfe26cefe019a5b9ddae56b` |

The mod is currently scoped and tested for **solo play**. Multiplayer behavior is not supported or claimed.

Game updates can change interop signatures, enum ranges, and serialization layouts. If the game build or `GameAssembly.dll` hash differs, treat the release as unverified until it has been rebuilt and retested.

## Installation

### 1. Install BepInEx

Download the tested [BepInEx Unity IL2CPP Windows x64 build](https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791%2Bb782da4.zip).

Extract its contents into the Chained Beasts game directory—the folder containing `ChainedBeasts.exe`. For a default Steam installation this is usually:

```text
C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts
```

Launch the game once and close it after reaching the main menu. This lets BepInEx generate its configuration and IL2CPP interop assemblies. See the official [BepInEx IL2CPP installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html) if the loader does not start.

### 2. Install the plugin

Download `ChainedBeasts.ModFoundation.dll` from the repository's [Releases page](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases) and copy it to:

```text
<Chained Beasts>\BepInEx\plugins\ChainedBeasts.ModFoundation.dll
```

No other plugin files or copied game assemblies are required.

Launch the game. A successful load appears in `BepInEx\LogOutput.log` as:

```text
Loading [ChainedBeasts.ModFoundation 0.7.3]
[ChainedBeasts.ModFoundation] smoke test loaded
```

## Configuration

BepInEx creates the configuration file after the first launch:

```text
<Chained Beasts>\BepInEx\config\local.chainedbeasts.modfoundation.cfg
```

Close and restart the game after changing a setting.

| Section | Setting | Default | Purpose |
|---|---|---:|---|
| `BulkUp` | `EnableBulkUp` | `true` | Enables Bulk Up. |
| `BulkUp` | `ForceBulkUpFirstOffer` | `false` | Places Bulk Up in the next eligible training offer until acquired. |
| `RunnersWraps` | `EnableRunnersWraps` | `true` | Enables Runner's Wraps. |
| `RunnersWraps` | `ForceRunnersWrapsNextOffer` | `false` | Replaces one eligible equipment-shop slot for testing. |
| `Leadfoot` | `EnableLeadfoot` | `true` | Enables the Leadfoot affliction. |
| `Leadfoot` | `ForceLeadfootNextOffer` | `false` | Replaces one non-water potion slot for testing. |
| `AdrenalGraft` | `EnableAdrenalGraft` | `true` | Enables Adrenal Graft. |
| `AdrenalGraft` | `ForceAdrenalGraftNextOffer` | `false` | Shows the free operation at the next eligible surgeon. |
| `DeveloperMenu` | `EnableDeveloperMenu` | `true` | Enables the solo testing overlay and helper hooks. |
| `DeveloperMenu` | `MenuKey` | `F1` | Changes the key that opens and closes the overlay. |
| `Diagnostics` | `EnableDiagnostics` | `false` | Enables the read-only main-menu lifecycle diagnostic. |

The `Force...` options are testing conveniences. They replace at most one eligible generated offer and still require the player to complete the normal interaction. Turn them off for ordinary play.

## Removing or updating the mod

Close the game before replacing or deleting the DLL.

To uninstall, remove:

```text
<Chained Beasts>\BepInEx\plugins\ChainedBeasts.ModFoundation.dll
```

The configuration file may also be deleted if its settings are no longer wanted.

Do not remove or disable the plugin while an active run contains custom content. Finish or abandon that run first. In particular:

- Leadfoot uses a version-sensitive checkpoint bridge.
- Bulk Up stores a compact run marker to prevent repeated health multiplication.
- Adrenal Graft ownership is recorded in the run's surgery history.
- Removing the plugin from such a checkpoint can leave unknown IDs without their effect hooks.

## Building from source

Requirements:

- Windows
- [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows) (`pwsh`)
- .NET 6 SDK, or a newer SDK capable of targeting .NET 6
- The tested BepInEx IL2CPP build installed in your own Chained Beasts directory
- One successful BepInEx launch so `BepInEx\interop` exists

This repository does **not** redistribute Chained Beasts, Unity, or BepInEx assemblies. The project references the copies in your local game installation with `Private=false`.

Set the game directory for the current PowerShell session:

```powershell
$env:CHAINED_BEASTS_GAME_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts'
```

Build and deploy with the game closed:

```powershell
pwsh -File .\scripts\build-and-deploy-plugin.ps1 -Configuration Release
```

For a non-default installation:

```powershell
pwsh -File .\scripts\build-and-deploy-plugin.ps1 `
  -Configuration Release `
  -GameRoot 'D:\Games\Steam\steamapps\common\Chained Beasts'
```

The script verifies every local reference, refuses to deploy while the game is running, builds the `net6.0` project, copies only the plugin DLL, and prints the deployed SHA-256.

Compile without deploying:

```powershell
dotnet build .\src\ChainedBeasts.ModFoundation\ChainedBeasts.ModFoundation.csproj -c Release
```

## Project structure

```text
src/ChainedBeasts.ModFoundation/
  Plugin.cs                          Configuration and feature isolation
  CustomContentIds.cs                Custom IDs and collision guards
  CustomLocalization.cs              Runtime localization boundaries
  CustomTranslations.cs              I2 term registration and 12-language text
  BulkUpFeature.cs                   Skill example
  Equipment/RunnersWrapsFeature.cs   Equipment example
  Afflictions/LeadfootFeature.cs     Potion-affliction example
  Surgeries/AdrenalGraftFeature.cs   Surgery example
  DevTools/DeveloperMenuFeature.cs   Solo testing overlay
  FeaturePatcher.cs                  Harmony target helper

scripts/
  build-and-deploy-plugin.ps1        Closed-game build/deploy workflow

docs/
  ADDING_CONTENT.md                  Contributor walkthroughs for each content type
  images/                            In-game screenshots used by this README
```

For implementation details and guidance on adding another skill, equipment item, affliction, or surgery, read [Adding custom content](docs/ADDING_CONTENT.md).

## Make your own mod

There are two different extension paths:

- **Add content to this plugin:** preserve the supplied IDs (`11000`, `2499`, `11010`, and `11020`), add new named constants in `CustomContentIds.cs`, give every item distinct localization keys, and retain the runtime collision guards. Persistent skills and afflictions also need their own verified storage strategy; changing only the content ID does not allocate a new checkpoint slot.
- **Create a separate plugin:** change the assembly name, root namespace, BepInEx plugin GUID/name/version, Harmony owner, configuration sections and keys, localization-key prefix, and every custom content ID. Do not ship references to your local game assemblies.

The examples are deliberately concrete rather than a generic content-loader API. Start with [Adding custom content](docs/ADDING_CONTENT.md), especially its checkpoint-storage warning and joint save/restore test, before copying a persistent skill or affliction.

## Known limitations

- Custom strings follow the game's selected language for English, Korean, Japanese, Brazilian Portuguese, Latin American Spanish, German, French, Italian, Simplified Chinese, Traditional Chinese, Russian, and Turkish. These community translations have not yet been reviewed by native speakers; unknown language names fall back to English.
- New icons are not implemented. All four examples reuse visual references already present in the running game.
- Custom IDs and checkpoint marker slots can conflict with another mod that independently claims the same values. Bulk Up's `SkillType.None` and Leadfoot's `InjuryType.EmptySlot1` bridges belong specifically to those supplied examples; copying either bridge does not create independent storage.
- Adrenal Graft cannot be appended to a native surgery registry because the tested game exposes none. The native generator constructs each visit's list internally, so normal mode replaces one otherwise eligible returned slot on a deterministic one-in-four visit while preserving the option count. “Deterministic” means the same run/player/visit state gives the same decision rather than rolling fresh random chance every time. The force option bypasses that gate for testing.
- Bulk Up's acquisition banner is confirmed fixed in the user's 0.7.3 solo test. The forced offer now updates its stored skill as well as the temporary display list, so selection, acquisition, and reveal use the same ID. The ineffective banner scanner and the earlier 0.7.1 native UI detours remain removed.
- Potion and skill rerolls passed the user's solo test. Shop refreshes appear in the log but still need explicit visual confirmation. Surgery rerolls animate the refresh but can leave all offers unchanged; this known issue is deferred.
- Removing Bulk Up with the developer menu explicitly resynchronizes its processed-level marker. Removal by another ordinary game mechanic does not use that helper and the remove/reacquire path remains unverified.
- Removing a skill through the developer menu does not reverse unrelated one-time side effects that skill may have performed when acquired.
- The developer menu is a testing tool, not a supported multiplayer admin interface.
- Every game update requires compatibility verification against newly generated BepInEx interop assemblies.

## Contributing

Keep changes narrow and reversible:

1. Use unused IDs with runtime collision guards.
2. Preserve native acquisition and ownership bookkeeping.
3. Do not commit game files, BepInEx binaries, generated interop assemblies, saves, logs, or build output.
4. Build with zero warnings and test presentation, acquisition, effect removal, checkpoint restore, fresh-run reset, and ordinary content around the new feature.
5. Document anything that remains untested, especially multiplayer behavior.

The repository's `.gitignore` is configured to exclude local analysis output, binaries, backups, logs, and user data.

## Disclaimer

This is an unofficial community project and is not affiliated with or endorsed by Featherweight Games. Chained Beasts and its assets belong to their respective owners. The project references assets already loaded by the user's installed game and does not redistribute them.
