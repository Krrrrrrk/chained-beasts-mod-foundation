# Play Terms & Contusions

**Terms & Contusions 0.8.3** is the experimental gameplay pack built on Chained Beasts Mod Foundation.

The current source builds **0.9.1**, available as an [experimental prerelease](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.9.1) with [multiplayer support](../MULTIPLAYER.md). The stable-install links below still install the Solo 0.8.3 release.

**Ready to play?** Follow the [quick install](../../README.md#quick-install), or use the [detailed installation and update guide](../INSTALLATION.md). The [recommended combined download](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.8.3/TermsAndContusions-with-Foundation-0.8.3.zip) includes both required mod DLLs. BepInEx is installed separately.

This page explains the content, settings and limitations. For source builds, use the [developer guide](../foundation/README.md).

<a id="what-exists-now"></a>

## What's included

| Category | Entries | Progression |
|---|---:|---|
| Skills | 12 | Four ranks per custom skill |
| Equipment | 12 | Effects while equipped |
| Potion afflictions | 11 | One to three skulls |
| Surgeries | 11 | Once per player per run |
| **Total** | **46** | |

Custom descriptions start with **`TC:`**. Content uses the game's normal offer and acquisition paths, so a custom item or skill is not guaranteed to appear on the first offer screen. Names and content IDs are unchanged by the description marker.

I have made no attempt to balance these additions. They may be overpowered, underpowered, or not work as intended. Treat this as an experimental solo pack.

<a id="complete-target-catalog"></a>

## Complete catalog

The [content catalog](CATALOG.md) lists names, effects, scaling and prices. The [manifest](manifest.json) maps all 46 entries to stable keys, numeric IDs, implementation files, configuration and source files.

All entries have implemented source; that does not mean every gameplay case has been verified. For the detailed mechanics and interactions, see:

| Category | Detailed guide |
|---|---|
| Skills and throwing | [Four-rank skills, Longshot and Counterweight Sling](SKILLS.md) |
| Equipment | [Prices, effects and per-battle allowances](EQUIPMENT.md) |
| Potion afflictions | [Severity, suppression, reduction and cures](AFFLICTIONS.md) |
| Surgeries | [Tradeoffs, prices, offers and completion](SURGERIES.md) |


### The four original examples

These additions retain their established identities in the expanded pack:

| Content | ID | Implemented behavior |
|---|---|---|
| Bulk Up | Skill `11000` | Four ranks: 2x/3x/4x/5x maximum HP. Actual rank changes preserve the health percentage. |
| Runner's Wraps | Equipment `2499` | Dodge stamina cost multiplied by 0.75 while equipped; price 40. |
| Leadfoot | Injury `11010` | Effective severity 1/2/3 reduces movement speed by 10/20/30%; normal potion healing is preserved. |
| Adrenal Graft | Surgery `11020` | Stamina regeneration multiplied by 1.25 for the run; price 0. |

## Configuration

After the first launch with the pack installed, edit:

`BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`

Close the game before editing and restart afterward. The catalog identifies each entry's setting. **Keep content enabled while a saved run owns it.**

Normal offers are the default. Leave `[DeveloperOffers] EnableForcedOffers = false` for normal play. Deliberately forcing one of the four original examples requires that master switch and the corresponding individual `Force*` option. Existing offers already stored in a run are not rewritten.

The first-load configuration migration imports the eight legacy content enable/force choices where no explicit destination value exists. The separate master switch prevents an old true Force setting from silently enabling forced offers. See [configuration migration](../foundation/CONFIGURATION_MIGRATION.md).

Foundation's settings and optional F1 tools remain in:

`BepInEx/config/local.chainedbeasts.modfoundation.cfg`

The F1 menu is for testing and can change real run data. Use a disposable run when experimenting. Skill and potion rerolls have worked in the author's solo tests; surgery rerolls are still unreliable.

## Saves, updates and removal

Read [the update instructions](../INSTALLATION.md#updating-an-existing-install) before replacing an older install.

Custom checkpoint files must stay paired with their matching native save. Back up the complete save folder and corresponding mod DLLs/configuration together. Missing or disabled owned content can block restoring a run, and the old monolithic 0.7.3 plugin cannot read the new checkpoint protocol.

For removal or a return to an older version, follow [uninstalling and rolling back](../INSTALLATION.md#uninstalling-and-rolling-back). The [persistence guide](../foundation/PERSISTENCE.md) explains the protocol for mod developers.

## Verification and known limitations

- The author reported a **crash-free 0.8.2 playtest** following the native hook fixes. Individual content entries and checkpoint cases were not exhaustively verified.
- **0.8.3** adds `TC:` descriptions and normal-offer defaults. Builds and nongame checks pass in the recorded release work; these changes still need a fresh gameplay check. See [release notes](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3).
- **Multiplayer is unsupported.** Developer actions and forced offers require the actual Solo session/authority checks.
- Original icons and models are not included; the pack reuses visuals already present in the installed game.
- New descriptions use English fallback. Previously supplied translations are retained but have not had native-speaker review.
- Automatic Steam Cloud synchronization of custom save files is not implemented. Full arena-instance and current-stamina restoration are also outside the supported save behavior.

## Build and installation

Players can install the [ready-made release](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3). To compile or modify the source, follow the [developer setup and build guide](../foundation/README.md#clone-and-build).

The production assemblies are `ChainedBeasts.ModFoundation.dll` and `TermsAndContusions.dll`. The content pack's assembly/namespace is `TermsAndContusions`; its BepInEx GUID is `local.chainedbeasts.termsandcontusions`. The current code declares Foundation `>=0.8.0 <0.9.1`; use the matching 0.8.3 release DLLs for installation.

Optional teaching examples are separate projects and are excluded from the normal packages and deployment. Game, loader, Unity, generated interop, extracted assets and saves are not redistributed. Original source and documentation remain MIT.
