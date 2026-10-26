# Configuration migration and legacy compatibility

Foundation and the content pack have separate configurations. Legacy content settings are imported once without changing the original content IDs. Back up the matching save, sidecars, DLLs and configuration before migrating.

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

The old Double HP Skills setting is not imported or revived. The pack includes individual skill and Counterweight Sling enable switches; it adds no new default-on forced offers or cheats. Menu shortcuts and diagnostics do not move, so no shortcut or enum conversion is required. The migration reader itself never edits the legacy file. Future normal Foundation config saving can reformat that file while retaining its unbound values.

## Existing content state

| Existing content | Preserved ID | Current storage |
|---|---:|---|
| Bulk Up | `SkillType 11000` | Skill dictionary; shared schema-1 rank/order; old None marker is import-only. Ranks 1–4 now give 2x/3x/4x/5x max HP. Old rank-one saves stay rank one/2x without an acquisition replay. |
| Runner's Wraps | `PurchaseType 2499` | Native equipment/history; price 40 and 25% lower dodge cost while equipped. |
| Leadfoot | `InjuryType 11010` | Injury dictionary; shared schema-1 severity/order; old EmptySlot1 marker is import-only. Effective severity 1–3. |
| Adrenal Graft | `SurgeryType 11020` | Native previousSurgeries history; free and 25% faster stamina recovery. Surgery handling uses a genuine native-completion receipt plus normal return/payment, replacing the old money-only heuristic. |

The split never remaps these IDs or their historical localization aliases. Modern skills/afflictions use one shared checkpoint discriminator plus external namespaced records, rather than one native slot per feature. The [persistence guide](PERSISTENCE.md) describes association, successful-write publication, migration-once, older checkpoints, disabled/missing content, and unknown schema behavior.

Restore/import assigns ownership without purchases, healing, gold changes, or grant calls. Existing Bulk Up rank one does not receive another HP grant on migration. Canonical Add/RemoveSkill before/after callbacks now cover normal removal and reacquisition; the old permanent processed-level cache and menu-only resynchronization are removed. Actual native execution, HP/HUD and serialization remain unverified.

Disabling an owned feature does not erase its record. Resuming such a run is refused until its compatible owner is enabled again. Foundation alone loads independently but cannot continue an old content-bearing run without the corresponding migration owner/pack. Finish/abandon that run or restore a matching pre-content backup before removing a pack.

## Backups and rollback

Back up the complete native user-data folder, custom sidecars, matching plugin DLLs and configuration together before migrating. The [installation guide](../INSTALLATION.md#uninstalling-and-rolling-back) covers removal and paired restoration. Restore ownership without replaying purchases or resource grants. A source-only rollback is separate from restoring a run.

## Normal offers

Legacy Force values are preserved as configuration data but require the separate `[DeveloperOffers] EnableForcedOffers` switch, which defaults to false. To deliberately force an example for testing, enable both the master and the relevant Force flag, then restart. Existing stored offers are not rewritten.
