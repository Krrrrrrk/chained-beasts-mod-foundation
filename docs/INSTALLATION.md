<a id="install-migrate-and-roll-back-083"></a>

# Install Terms & Contusions

**Want to play with the custom content? Install BepInEx once, then install the combined Terms & Contusions + Foundation package.** You do not need to download the source code or compile anything.

[Quick install on the main page](../README.md#quick-install) · [Updating an existing install](#updating-an-existing-install) · [Troubleshooting](#troubleshooting) · [Make your own mod](foundation/README.md)

These instructions are for the **Windows 64-bit Steam version** of Chained Beasts. The current release is **0.8.3**, for **Solo play**. [Back up your install and saves](#back-up-your-install-and-saves) before adding mods or changing versions.

The current source branch builds **0.9.1**, available as an [experimental multiplayer prerelease](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.9.1).
For its matching DLLs, party requirements and pending tests, use
[the multiplayer guide](MULTIPLAYER.md). The release links below remain 0.8.3.

Already using an older version of this mod? Follow [Updating an existing install](#updating-an-existing-install) first.

## Fresh install: play Terms & Contusions

### 1. Find the game folder and close the game

In your Steam Library, right-click **Chained Beasts → Manage → Browse local files**.

Keep that File Explorer window open. This is the **game folder**: it contains `ChainedBeasts.exe`. Its default location is:

```text
C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts
```

Your Steam library may be on another drive. Use the folder Steam opens, and close the game before copying files.

### 2. Install BepInEx once

**If you already have the working BepInEx 6 IL2CPP setup for this game, skip to step 3.**

1. Download the [tested BepInEx 6.0.0-be.791+b782da4 **Unity IL2CPP win-x64** ZIP](https://builds.bepinex.dev/projects/bepinex_be/791/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.791%2Bb782da4.zip).
2. Extract **all contents** of that ZIP into the game folder, beside `ChainedBeasts.exe`. Do not leave them inside an extra folder named after the download.
3. Launch the game normally. **The first launch can take longer** while BepInEx generates the files it needs. Wait until the main menu appears.
4. Exit the game normally before installing the mod.

You should now have a `BepInEx` folder in the game folder, with generated files such as `BepInEx\config` and `BepInEx\interop`. If that did not happen, check the extraction location before continuing.

The download above is the loader used with this project. BepInEx 5 and Unity Mono packages are different builds. See the [official IL2CPP installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html) for more loader setup help.

### 3. Install the combined mod package

1. Download [**TermsAndContusions-with-Foundation-0.8.3.zip**](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.8.3/TermsAndContusions-with-Foundation-0.8.3.zip).
2. Extract the ZIP somewhere convenient, then open the extracted folder.
3. Copy its **`BepInEx` folder into the game folder** and allow the folders to merge.

The combined package contains **both required mod DLLs**. You do not need the other mod downloads as well. It does **not** include BepInEx itself; that is step 2.

Check that these two files now exist:

```text
Chained Beasts\BepInEx\plugins\ChainedBeasts.ModFoundation.dll
Chained Beasts\BepInEx\plugins\TermsAndContusions.dll
```

If they ended up under `BepInEx\BepInEx\plugins` or inside a folder named after the ZIP, they are in the wrong place.

On GitHub, choose the named combined ZIP under the release's **Assets**. The **Source code** downloads are for developers.

### 4. Launch and play Solo

Start the game normally and play **Solo**. Custom skills, equipment, potion afflictions, and surgeries appear through the normal offer systems. They are not all granted at startup or guaranteed in every offer.

Look for **`TC:` at the beginning of custom descriptions** to identify Terms & Contusions content.

The optional **F1** panel is for testing; you do not need to use it to play. See [configuration](#configuration) if you want to change settings or disable that panel.

## Back up your install and saves

With the game closed, copy these folders to a safe location **outside the game folder**:

| What to copy | Where to find it |
|---|---|
| All installed plugins | `BepInEx\plugins` inside the game folder |
| Mod settings | `BepInEx\config` inside the game folder |
| The complete game save folder | `%USERPROFILE%\AppData\LocalLow\Featherweight Games\ChainedBeasts` |

For the save folder, press **Win + R**, paste the path from the table, and press Enter. Copy the **whole folder**, including `ModFoundation\checkpoints` if it exists. A folder that has not been generated yet does not need backing up.

Keep those copies together and label them with the date and mod version. Do not put backup DLLs under `BepInEx\plugins`: the loader also scans its subfolders.

Custom checkpoint files and the game's `SaveData` belong together. Automatic Steam Cloud synchronization of the custom files is not implemented, so keep your own complete backup. Moving to another computer or restoring only a cloud save can leave required custom checkpoint files behind. The [persistence guide](foundation/PERSISTENCE.md) explains the technical details.

## Updating an existing install

1. **Close the game and make the complete backup above.**
2. Open `BepInEx\plugins` and check its subfolders for older copies of **`ChainedBeasts.ModFoundation.dll`** and **`TermsAndContusions.dll`**. Remove those old copies from the active plugin folder after backing them up. Also remove renamed copies you previously made of these two assemblies. Leave unrelated mods alone.
3. Install the new **combined package** as described in [step 3](#3-install-the-combined-mod-package). Use the two DLLs from the same release.
4. Keep your existing configuration files. Start the game and check for load errors if anything looks wrong.

**Coming from 0.7.3?** That version put the custom content inside a single Foundation DLL. Version 0.8.3 uses two DLLs. Replace the old Foundation and install the content-pack DLL alongside the new one; do not leave the old copy in another plugin subfolder.

The mod imports supported legacy ownership and settings when loading. Old developer offer-forcing settings only take effect when the separate `EnableForcedOffers` master setting is enabled. Leave that master setting false for normal play.

A backup is especially important for this change: **0.7.3 cannot read the newer custom checkpoint format**. Downgrading requires the matching older plugins, settings, and saves together.

<a id="first-manual-startup-and-configuration"></a>

## Configuration

The mod generates its settings on first startup. Close the game before editing a configuration file, then restart it to apply your changes.

| File, relative to the game folder | Controls |
|---|---|
| `BepInEx\config\local.chainedbeasts.termsandcontusions.cfg` | Content enables, prices where configurable, and developer offer overrides |
| `BepInEx\config\local.chainedbeasts.modfoundation.cfg` | The optional F1 panel, its key, and diagnostics |

For normal play:

- Keep `[DeveloperOffers] EnableForcedOffers = false` in the content-pack configuration.
- To disable the F1 panel, set `[DeveloperMenu] EnableDeveloperMenu = false` in the Foundation configuration.
- Leave content enabled while your saved run owns it. Disabling an owned entry can prevent that run from loading.

The F1 panel starts with its cheats off. Its actions can change actual run data, so use a disposable run when experimenting. Skill and potion rerolls have worked in the author's solo tests; surgery rerolls can animate without changing the options.

See the [catalog](terms-and-contusions/CATALOG.md) for individual content settings.

## Troubleshooting

| Problem | What to check |
|---|---|
| No `BepInEx\config` folder after startup | Confirm the Windows IL2CPP x64 loader contents are beside `ChainedBeasts.exe`, then let the first launch finish. |
| No custom content or pack configuration | Check that **both** mod DLLs are in `BepInEx\plugins` and that you downloaded the combined mod ZIP, not Source code. Check the log for startup errors. |
| Only the F1 panel appears | Foundation can run by itself. Confirm `TermsAndContusions.dll` is installed and loaded too. |
| The mod loads, but an offer contains no custom item | Offers use the normal pools. A particular offer need not contain custom content; look for `TC:` descriptions as you play. |
| Duplicate-plugin or dependency errors | Check every plugin subfolder for old/renamed copies. Reinstall the matching pair of DLLs from the combined package. |
| A run refuses to load after an update or removal | Keep the saved run's required content enabled and restore its matching custom checkpoint files if needed. Do not delete checkpoint files to bypass the error. |
| A crash, missing-method error, or failure after a game update | Save the fresh log and note the game, loader, and mod versions. Report those details before changing loaders or editing saves. |

The project's setup writes its log to **`BepInEx\LogOutput.log`**, inside the game folder. After a fresh launch, look for both plugin names and any errors following them:

```text
ChainedBeasts.ModFoundation 0.8.3
Terms & Contusions 0.8.3
```

Loading lines are useful diagnostics; they do not establish that every feature works. Foundation must initialize successfully for the pack to run.

<a id="saves-and-rollback"></a>

## Uninstalling and rolling back

### Remove the content pack

Finish or abandon the custom run **while the mod is still installed**. Then close the game, back up your files, and remove `TermsAndContusions.dll` from the plugin folder.

Keep Foundation if another installed mod requires it. Otherwise, you can also remove `ChainedBeasts.ModFoundation.dll`. You do not need to remove BepInEx or unrelated mods.

An older saved run that owns removed or disabled custom content may no longer load. To return to a pre-mod or older-version run, use its complete matching backup.

### Restore an older version

With the game closed, first back up your **current** plugins, settings, and entire save folder. Then replace the active plugin, configuration, and save folders with the older backup's matching copies.

Restoring a save backup also restores the progress at that backup's time. A whole plugin-folder backup restores other mods to that same point as well. Do not mix an old `SaveData` with unrelated newer `ModFoundation\checkpoints` files.

For source-built installations with a script-created backup, an advanced rollback command is included below.

## Requirements and package choices

The inspected setup is **Windows x64**, the **Steam game version 1.0.1**, **Unity 6000.3.21f1**, and **BepInEx 6.0.0-be.791+b782da4 Unity IL2CPP x64**. Support for other game builds, platforms, and multiplayer is not established.

All packages are available on the [0.8.3 release page](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3).

| Download | Includes | Choose it when… |
|---|---|---|
| **`TermsAndContusions-with-Foundation-0.8.3.zip`** | Both mod DLLs | **You want to play Terms & Contusions.** |
| `ChainedBeasts.ModFoundation-0.8.3.zip` | Foundation only | You need the shared infrastructure for a different Foundation-based mod. |
| `TermsAndContusions-0.8.3.zip` | Content pack only | You already have matching Foundation installed. The combined ZIP is simpler for most players. |

Use matching **0.8.3** DLLs for this release. Packages also contain installation/license notices and a checksum manifest. They exclude the loader, game files, extracted assets, saves, configuration files, and optional teaching examples.

**Testing status:** the author reported a crash-free **0.8.2** playtest. The **0.8.3** description tags and normal-offer changes are build checked and still need a fresh in-game check. Individual mechanics, interactions, and save/restore cases are not exhaustively verified. See the [release notes](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3).

<a id="fresh-checkout-and-local-workflow"></a>

## Building and scripted installation

To compile the code or create your own mod, start with the [Foundation developer guide](foundation/README.md) and [separate-mod walkthrough](foundation/SEPARATE_MOD.md). The ZIP installation above does not require those tools.

<details>
<summary>Advanced: deploy a local build or restore a script-created backup</summary>

Run these commands from a checkout after following the developer build instructions. Replace the example game path with your actual installation.

```powershell
pwsh -NoProfile -File .\scripts\build-and-deploy-plugin.ps1 -Deploy -SkipBuild `
  -GameRoot 'D:\Games\Chained Beasts' `
  -UserDataRoot "$env:USERPROFILE\AppData\LocalLow\Featherweight Games\ChainedBeasts"
```

Omit `-SkipBuild` to build first. The script requires `-Deploy`, refuses while the game is running, checks the inspected game build, and creates a backup before replacing either of this project's DLLs. It detects old copies by assembly identity, including renamed/nested copies, while preserving unrelated plugins during installation. A partial-copy failure restores the previous owned DLLs.

The backup and `artifacts/packages/0.8.3/deployment.json` record the installation and hashes. The inspected `GameAssembly.dll` SHA-256 is `72f2892607b0772470caae5db766dc07aaf636c3edfe26cefe019a5b9ddae56b`. A changed build requires reinspection.

For a backup created by that workflow:

```powershell
$backupPath = 'D:\path\to\your\recorded\backup'
pwsh -NoProfile -File .\scripts\restore-plugin-backup.ps1 -Restore `
  -BackupRoot $backupPath `
  -GameRoot 'D:\Games\Chained Beasts' `
  -UserDataRoot "$env:USERPROFILE\AppData\LocalLow\Featherweight Games\ChainedBeasts"
```

**This rollback restores the entire recorded plugin, configuration, and game save folders.** It first backs up the current set. It requires matching roots/schema and verifies the backup's file hashes. Use the backup path actually recorded for your installation, not a newly created or incomplete folder.

Older backups without `checkpoint.json` require manual restoration of their complete matching files. Source-code restoration is separate from plugin/save rollback. None of these scripts launches the game.

</details>
