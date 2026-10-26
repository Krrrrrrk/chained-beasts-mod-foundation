# Install, migrate and roll back 0.8.3

This is a **local experimental build awaiting manual Solo testing**, not a published or gameplay-verified release. v0.7.3 is the last manually tested monolithic version. The 0.8.3 split has two production plugins; optional teaching examples are excluded from packages and normal deployment.

> The author reported a crash-free 0.8.2 gameplay test following the earlier native-hook fixes. 0.8.3 adds description tags and normal-offer defaults; those changes still need an in-game check. See [current notes](COMMUNITY_UPDATE_0.8.3.md). Individual mechanics/checkpoint cases are not all verified.

## Requirements and package choices

Use the inspected Steam Windows x64 Chained Beasts **1.0.1**, Unity **6000.3.21f1**, BepInEx **6.0.0-be.791+b782da4 Unity IL2CPP x64**, and its generated runtime interop. GameAssembly SHA-256: `72f2892607b0772470caae5db766dc07aaf636c3edfe26cefe019a5b9ddae56b`. A game update requires reinspection; don't upgrade a working loader as a speculative fix. BepInEx/.NET runtime/game files are installed separately, never included in these ZIPs.

| Local ZIP in `artifacts/packages/0.8.3` | Included DLLs | Use |
|---|---|---|
| `ChainedBeasts.ModFoundation-0.8.3.zip` | Foundation | Reusable infrastructure and optional F1 tools, no pack content |
| `TermsAndContusions-0.8.3.zip` | Content pack | Requires matching Foundation `>=0.8.0 <0.9.0` |
| `TermsAndContusions-with-Foundation-0.8.3.zip` | Both | Convenient complete player installation |

Each ZIP contains only the intended DLL(s) under `BepInEx/plugins/`, an installation README, MIT license, dependency notices and a SHA-256 package manifest. No game/Unity/BepInEx DLLs, interop, extracted assets, configurations, saves, logs or example assemblies. Foundation GUID remains `local.chainedbeasts.modfoundation`; pack GUID is `local.chainedbeasts.termsandcontusions`.

## Fresh checkout and local workflow

Install Git, PowerShell 7 (`pwsh`) and a .NET SDK/targeting pack that supports **net6.0**. Loader first-run interop generation must already have completed through normal manual game startup. Do not use Cpp2IL inspection stubs as runtime references. Then:

```powershell
git clone https://github.com/Krrrrrrk/chained-beasts-mod-foundation.git
cd chained-beasts-mod-foundation
$env:CHAINED_BEASTS_GAME_DIR = 'D:\Games\Chained Beasts'
pwsh -NoProfile -File .\scripts\build-plugins.ps1
pwsh -NoProfile -File .\scripts\test-stage7.ps1
pwsh -NoProfile -File .\scripts\package-plugins.ps1 -SkipBuild
```

Until this local work is separately published, a fresh remote checkout still contains the older public version. These commands apply to the **0.8.3 source revision**, not the old tag. `-GameRoot` overrides the environment variable; omitted paths fall back to the standard Steam installation. Build Foundation alone with `-FoundationOnly`. Checks build the independent examples too but never load game assemblies, execute live handlers, launch or deploy.

Explicit local deployment, with the game closed:

```powershell
pwsh -NoProfile -File .\scripts\build-and-deploy-plugin.ps1 -Deploy -SkipBuild `
  -GameRoot 'D:\Games\Chained Beasts' `
  -UserDataRoot "$env:USERPROFILE\AppData\LocalLow\Featherweight Games\ChainedBeasts"
```

Without `-SkipBuild`, it builds first. Without **`-Deploy`**, it refuses before installation changes. If the game is running it stops without terminating it. It checks the inspected native build, backs up source/current DLLs/configuration/full user data outside loader scan directories, verifies copied hashes, removes every prior DLL with our two assembly identities (including renamed/nested copies), then copies Foundation before pack. Unrelated plugins remain untouched. Partial-copy failure restores prior owned DLLs. The checkpoint and `artifacts/packages/0.8.3/deployment.json` identify the exact files/hashes/rollback location; this does not prove runtime success.

For a manual ZIP install: close the game, make the same complete backup, remove old copies of our DLLs from **every** plugin subfolder, then extract the package's BepInEx folder into the game root. Do not leave the old content-bearing Foundation beside the split. Do not install optional examples unintentionally.

## First manual startup and configuration

Manually launch to the main menu, inspect fresh `BepInEx/LogOutput.log`, and exit normally. Expected source markers include:

```text
Loading [ChainedBeasts.ModFoundation 0.8.3]
[Foundation] checkpoint bridge installed; sidecars publish only after successful native platform writes
Loading [Terms & Contusions 0.8.3]
[Terms & Contusions] content pack loaded; shared identities and checkpoint persistence enabled
```

These are expected markers, **not logs observed during this implementation**. Foundation must initialize before the pack; missing/incompatible/failed shared infrastructure refuses pack startup. A collision disables/refuses content rather than renumbering it. Preserve fresh logs on any MissingMethod, interop, registration or restore error and stop that test instead of experimenting with loader updates or editing saves. Existing Class::Init signature and native warning context is documented in earlier reports; new errors must be distinguished from them.

Foundation config: `BepInEx/config/local.chainedbeasts.modfoundation.cfg`. Pack config: `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`. First normal startup imports the eight original enable/force booleans once, preserving explicit destination values and the four original IDs. The generic deployment helper does not edit configs or saves. **Normal offers are the default:** `[DeveloperOffers] EnableForcedOffers` is false, so migrated true Force flags are ineffective unless that separate testing switch is explicitly enabled. The local 0.8.3 update also clears the four old Force flags at the author's request. Active cheats remain off by default. Restart after changing enable/price/offer settings. The [catalog](terms-and-contusions/CATALOG.md) names each setting. F1 is configurable; cheats clear outside real Solo. The custom-catalog filter does not change acquisition rules.

## Saves and rollback

`SaveData` and `ModFoundation/checkpoints` are a matched pair, not two independently interchangeable saves. Back up the **entire** LocalLow user-data directory plus the matching DLL/configuration set. Sidecar schema 1 binds run/player identities, generation and exact native checkpoint fingerprint; immutable archives allow older paired backups. Missing/mismatched state, unavailable owned content or unknown schemas refuse restoration and preserve files. Skills/afflictions use independent namespaced rank/order records; native equipment and surgery histories remain authoritative. Legacy Bulk Up/Leadfoot markers import once without resource grants. Never copy those private marker slots for a second feature.

Old **v0.7.3 cannot read the new protocol**. Removing only the pack, disabling owned entries, or downgrading only Foundation is insufficient. Finish/abandon the custom run before removing its owner, or restore a complete pre-expansion checkpoint. Source-only restoration is separate: copy source/docs/scripts/tests/examples from the recorded backup if needed; do not reset away newer work.

For a checkpoint produced by the new deployment script, the explicit rollback helper restores the **entire recorded plugin/config/user-data set**, removing newer unmatched sidecars; it first backs up the current set so the rollback is itself recoverable:

```powershell
pwsh -NoProfile -File .\scripts\restore-plugin-backup.ps1 -Restore `
  -BackupRoot 'D:\chained-beasts-modding\backups\expansion-stage7-deploy-<timestamp>' `
  -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts' `
  -UserDataRoot "$env:USERPROFILE\AppData\LocalLow\Featherweight Games\ChainedBeasts"
```

The selected checkpoint must match the requested absolute roots/schema and every saved file hash. The game must be closed. This helper never launches it. Older stage backups predate `checkpoint.json`: restore their full saved directories manually as a matching set. Keep backups outside `BepInEx/plugins`, which scans recursively. Automatic Steam Cloud synchronization of sidecars is not implemented; a cloud-only native save restore can require its corresponding local sidecars.

Next is the user's [manual Solo checklist](MANUAL_TESTS.md), not automatic gameplay. New English fallback, reused visuals, deferred surgery rerolls and incomplete arena-instance/current-stamina restoration are documented limitations; see [handoff](STAGE7_HANDOFF.md).
