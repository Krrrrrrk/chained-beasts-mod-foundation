# Configuration migration and legacy compatibility

Stage 7 deployed both 0.8.0 DLLs but did not run the game: configuration/save migration remains awaiting normal manual startup. Existing config and user-data bytes remain unchanged. See [exact hashes and rollback checkpoint](../STAGE7_HANDOFF.md).

## Configuration ownership

Foundation keeps its existing identity and configuration:

```text
BepInEx/config/local.chainedbeasts.modfoundation.cfg
```

`Diagnostics.EnableDiagnostics`, `DeveloperMenu.EnableDeveloperMenu`, and `DeveloperMenu.MenuKey` stay there. The inspected BepInEx `ConfigFile.Save()` retains unbound/orphaned entries, so Foundation binding its remaining settings does not erase the old content entries before the pack reads them. This behavior is exercised against the actual installed BepInEx.Core in the nongame checks.

Terms & Contusions has its own configuration identity:

```text
BepInEx/config/local.chainedbeasts.termsandcontusions.cfg
```

On the pack's first eventual load, `TermsAndContusions.Plugin.Load()` binds `Migration.LegacyConfigurationImported`, reads a legacy snapshot only if that flag is false, binds the eight allowlisted content booleans, sets the flag, and saves the pack configuration. Autosaving is suppressed while those settings are assembled and restored afterward. No game ownership, purchases, healing, or grants are invoked by configuration import.

| Imported section | Keys | Fresh defaults |
|---|---|---|
| `BulkUp` | `EnableBulkUp`, `ForceBulkUpFirstOffer` | `true`, `false` |
| `RunnersWraps` | `EnableRunnersWraps`, `ForceRunnersWrapsNextOffer` | `true`, `false` |
| `Leadfoot` | `EnableLeadfoot`, `ForceLeadfootNextOffer` | `true`, `false` |
| `AdrenalGraft` | `EnableAdrenalGraft`, `ForceAdrenalGraftNextOffer` | `true`, `false` |

An explicit existing pack value takes precedence. Otherwise a valid legacy boolean becomes its initial value. A disabled legacy feature remains disabled; an intentionally enabled testing override remains enabled for that migrating user. Absent or malformed legacy settings use the normal defaults, which never turn a new force option on. Once the marker is set, later legacy-file edits are ignored. Change the pack configuration for future launches. Do not routinely clear the migration flag.

The old Double HP Skills setting is not imported or revived. Stage 3 adds eleven skill enable switches and Counterweight Sling's enable switch to the pack; it adds no new default-on forced offers or cheats. Menu shortcuts and diagnostics do not move, so no shortcut or enum conversion is required. The migration reader itself never edits the legacy file. Future normal Foundation config saving can reformat that file while retaining its unbound values.

## Existing content state through Stage 6

| Existing content | Preserved ID | Current storage |
|---|---:|---|
| Bulk Up | `SkillType 11000` | Skill dictionary; shared schema-1 rank/order; old None marker is import-only. Ranks 1–4 now give 2x/3x/4x/5x max HP. Old rank-one saves stay rank one/2x without an acquisition replay. |
| Runner's Wraps | `PurchaseType 2499` | Native equipment/history; price 40 and 25% lower dodge cost while equipped. |
| Leadfoot | `InjuryType 11010` | Injury dictionary; shared schema-1 severity/order; old EmptySlot1 marker is import-only. Effective severity 1–3. |
| Adrenal Graft | `SurgeryType 11020` | Native previousSurgeries history; free and 25% faster stamina recovery. Stage 6 uses a genuine native-completion receipt plus normal return/payment, replacing the old money-only heuristic. |

The split never remaps these IDs or their historical localization aliases. Modern skills/afflictions use one shared checkpoint discriminator plus external namespaced records, rather than one native slot per feature. The [persistence guide](PERSISTENCE.md) describes association, successful-write publication, migration-once, older checkpoints, disabled/missing content, and unknown schema behavior.

Restore/import assigns ownership without purchases, healing, gold changes, or grant calls. Existing Bulk Up rank one does not receive another HP grant on migration. Canonical Add/RemoveSkill before/after callbacks now cover normal removal and reacquisition; the old permanent processed-level cache and menu-only resynchronization are removed. Actual native execution, HP/HUD and serialization remain unverified.

Disabling an owned feature does not erase its record. Resuming such a run is refused until its compatible owner is enabled again. Foundation alone loads independently but cannot continue an old content-bearing run without the corresponding migration owner/pack. Finish/abandon that run or restore a matching pre-content backup before removing a pack.

## Backups and rollback

Backups are outside all loader scan paths and ignored by Git:

```text
D:\chained-beasts-modding\backups\expansion-stage1-20261025-121742
D:\chained-beasts-modding\backups\expansion-stage2-20261025-124225
D:\chained-beasts-modding\backups\expansion-stage3-20261025-165045
D:\chained-beasts-modding\backups\expansion-stage4-20261025-182500
D:\chained-beasts-modding\backups\expansion-stage5-20261025-185657
```

These checkpoints preserve the corresponding source/docs/scripts and active installation/configuration/user data; Stage 2 also preserves the existing Stage 1 work before infrastructure changes. Each has a SHA-256 manifest and source commit/patch. None is a distributable. A fresh matching backup is still required before final Stage 7 deployment.

No installed files or user-data migration have been changed by these stages. For source-only rollback, inspect/copy the relevant checkpoint in a separate directory rather than resetting away later uncommitted work.

After a future actual deployment, close the game, remove the pack/example DLLs from all plugin scan locations, restore the backed-up old Foundation DLL and configuration, then restore the **entire corresponding user-data backup**. For modern expanded runs, native SaveData and `ModFoundation/checkpoints` must always travel together. The old v0.7.3 plugin cannot interpret a new discriminator/sidecar checkpoint; downgrading just DLLs against new saves is unsupported. Do not manually edit saves to bypass migration or restore refusal.

Stage 3 adds validated remaining-duration/charge/reservoir state under `terms-and-contusions:skills/combat-v1`. Capture refuses unsettled native HP receipts and pending resource reservations; unsupported feature schemas block restore before native mutation. The [Stage 3 guide](../terms-and-contusions/STAGE3_SKILLS.md) lists new switches, identities, price and manual checks. Downgrading to Stage 2 source/DLLs cannot resume a save that owns the new skills/sling without their matching owner; use the complete matching older user-data backup.

Stage 4 adds ten `[Equipment] Enable<Name>` startup switches, default true; existing wraps/sling choices are unchanged. Its validated `terms-and-contusions:equipment/battle-v1` state preserves paid/spent/reward/physical-allowance tokens and eligible aim time without replaying resources. New equipment text uses English fallback. See [identities, prices, switches and rollback limits](../terms-and-contusions/STAGE4_EQUIPMENT.md), including native arena objects not being part of a run checkpoint.

Stage 5 adds ten `[Afflictions] Enable<Name>` startup switches, default true; existing Leadfoot choices remain unchanged. Raw severity/order uses the shared checkpoint records, and validated `terms-and-contusions:afflictions/battle-v1` stores bounded timers/counters/focus without resource replay. New text uses English fallback. Native checkpoints omit live stamina/recovery and arena objects: no old Lead Belly tail is replayed, and an unavailable focus is cleared explicitly. See [affliction configuration, persistence limits and manual checks](../terms-and-contusions/STAGE5_AFFLICTIONS.md).

See [persistence and manual checks](PERSISTENCE.md) and [stage progress](../EXPANSION_PROGRESS.md). No intermediate deployment is permitted.

Stage 6 preserves the existing `[AdrenalGraft]` booleans and its free price, including the migrated force choice. Ten new `[Surgeries] Enable<Name>` switches default true. Ten `[SurgeryPrices] <Name>` positive gold values use the defaults in [the surgery table](../terms-and-contusions/STAGE6_SURGERIES.md); restart to apply. No runtime configuration was migrated or written during implementation. Disabling an owned surgery refuses its checkpoint restore rather than silently dropping it. Stage 6 backup `backups/expansion-stage6-20261025-200242` preserves pre-change source/builds, installed v0.7.3/configuration and full user data. Nothing is deployed; after a future migration restore the entire paired DLL/config/user-data set to roll back.

Stage 7 final closed-game deployment is complete. Use the exact paired checkpoint and rollback command in [STAGE7_HANDOFF.md](../STAGE7_HANDOFF.md) and [INSTALLATION.md](../INSTALLATION.md); neither source/package preparation nor installation establishes successful native startup or migration.

## Normal offers from 0.8.3

The old Force values are still imported/preserved as configuration data, but are now gated by the separate `[DeveloperOffers] EnableForcedOffers` setting, which defaults to false. Normal play therefore includes the original four items only through their normal eligible pools. To deliberately force an example for testing, enable the master switch and its Force flag, then restart. Existing saved offers are not rewritten. The local author-requested 0.8.3 update clears all four Force flags as well.
