# Chained Beasts Mod Foundation + Terms & Contusions

I built this as a small starter kit for making custom content in **Chained Beasts**. It includes a reusable mod foundation and my example content pack, **Terms & Contusions**. Feel free to look through the code, copy an example, and make something of your own.

**I made zero attempt to balance these additions.** They might be wildly overpowered or just bad. They're examples of what we can do, not a carefully balanced expansion.

Every custom description starts with **`TC:`**, so it's easy to tell my additions apart from vanilla content. Names and internal IDs stay unchanged.

## What's included

| Project | What it does |
|---|---|
| **Mod Foundation** | Shared registration, localization, stats/combat events, checkpoint state and optional solo F1 tools for other mods to build on. |
| **Terms & Contusions** | **46 additions:** 12 four-rank skills, 12 equipment items, 11 potion afflictions with three severities, and 11 once-per-run surgeries. |

Bulk Up, Runner's Wraps, Leadfoot and Adrenal Graft are still here. They now sit in the normal offer pools alongside the other additions; they're not forced into your choices by default. Normal eligibility, ownership and costs still apply. Adrenal Graft is still free.

See the [complete catalog](docs/terms-and-contusions/CATALOG.md) for the effects and the [content manifest](docs/terms-and-contusions/manifest.json) for IDs, configuration and source files.

## Install

Download a **matching version** from [GitHub Releases](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases), then follow the [installation, migration and rollback guide](docs/INSTALLATION.md). Source on `main` is **0.8.3**. If that version isn't listed in Releases yet, build it from source below. The older 0.7.3 download uses a single plugin and its [archived instructions](docs/history/README-v0.7.3.md).

The current pack uses **two DLLs**: `ChainedBeasts.ModFoundation.dll` and `TermsAndContusions.dll`. Install both under `BepInEx/plugins` with the game closed. Remove older copies of our plugin, including renamed copies or copies in subfolders. Foundation can also be used on its own without my content pack.

The inspected setup is Windows x64, Chained Beasts **1.0.1**, Unity **6000.3.21f1**, and **BepInEx 6.0.0-be.791+b782da4 Unity IL2CPP x64**. Packages don't include BepInEx, game files or generated runtime bindings.

**Back up your complete saves and matching mod configuration before upgrading.** Custom checkpoint data must stay paired with its native save. Downgrading or removing content from an active run needs the matching backup, not just a DLL swap. Details are in the [persistence guide](docs/foundation/PERSISTENCE.md).

## Configuration and F1 tools

The pack's settings are in `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`. Individual entries can be enabled or disabled there; restart to apply changes. Keep content enabled while a saved run owns it.

Normal offers are the default. The old four `Force*` options only work when `[DeveloperOffers] EnableForcedOffers = true` as well. Leave that master switch false for normal play, even when upgrading a config that still contains old testing flags.

Foundation's optional **F1** menu has god mode, infinite stamina, healing, gold adjustments, skill/injury rank controls, a custom-content filter and offer rerolls. Open an offer screen before using the reroll button. These tools require a solo session with one local host/player and can change real checkpoint data, so I recommend a disposable test run.

Potion and skill rerolls have worked in my solo tests. **Surgery rerolls are still unreliable:** the refresh can animate without changing the options. I've left that issue for later. Multiplayer isn't supported.

## Build and make your own mod

You'll need Git, **PowerShell 7** (`pwsh`), a .NET SDK/targeting pack for **net6.0**, and the installed game's BepInEx-generated interop assemblies. Don't use Cpp2IL inspection stubs as runtime references. Set `CHAINED_BEASTS_GAME_DIR` or pass `-GameRoot` for another install location.

```powershell
pwsh -NoProfile -File .\scripts\build-plugins.ps1
pwsh -NoProfile -File .\scripts\test-stage7.ps1
pwsh -NoProfile -File .\scripts\package-plugins.ps1 -SkipBuild
```

ZIPs are written to `artifacts/packages/0.8.3`. Build/check commands don't launch the game or deploy. The explicit `scripts/build-and-deploy-plugin.ps1 -Deploy` workflow checks that the game is closed and makes a complete rollback checkpoint before replacing our DLLs.

Start with [the Foundation guide](docs/foundation/README.md), [separate plugin setup](docs/foundation/SEPARATE_MOD.md), or a complete example for a [skill](docs/foundation/tutorials/SKILL.md), [equipment item](docs/foundation/tutorials/EQUIPMENT.md), [affliction](docs/foundation/tutorials/AFFLICTION.md) or [surgery](docs/foundation/tutorials/SURGERY.md).

For a separate mod, change the plugin GUID, assembly/namespace, localization keys, content keys and numeric IDs. When adding content inside this pack, add new identities without changing the existing ones. Use the shared checkpoint API for independent state; copying an old reserved save slot won't create separate storage.

## Testing and limitations

I played **0.8.2 without encountering any crashes** after fixing the earlier hook problems. That doesn't mean I've verified every rank, interaction or save/resume case across all 46 additions. The 0.8.3 description tags and normal-offer update are build checked and still need a fresh in-game check. The [manual checklist](docs/MANUAL_TESTS.md) and [change notes](docs/COMMUNITY_UPDATE_0.8.3.md) track that distinction.

**I haven't implemented custom icons or models.** Everything reuses visual references already present in the game. New text has an English fallback; the older examples retain their supplied translations, which haven't had native-speaker review. Automatic Steam Cloud synchronization of custom sidecars and full arena/current-stamina restoration aren't supported.

## Screenshots

These are from my earlier four-example build; they show the basic cards and F1 tools rather than the full current catalog.

| Bulk Up | Runner's Wraps |
|---|---|
| ![Bulk Up trainer offer](docs/images/bulk-up-offer.png) | ![Runner's Wraps shop offer](docs/images/runners-wraps-shop.png) |

| Leadfoot | Adrenal Graft |
|---|---|
| ![Leadfoot potion](docs/images/leadfoot-potion.png) | ![Adrenal Graft surgery](docs/images/adrenal-graft-surgery.png) |

![My optional solo developer menu](docs/images/developer-menu.png)

## License

My original project code and documentation are under [MIT](LICENSE). BepInEx, HarmonyX, Unity, IL2CPP bindings and the game keep their own licenses; see [third-party notices](THIRD_PARTY_NOTICES.md). Game/runtime binaries, generated bindings, extracted assets, saves, backups and private logs aren't included in source control or packages.

This is an unofficial project, unaffiliated with Featherweight Games.
