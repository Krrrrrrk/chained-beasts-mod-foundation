# Play Terms & Contusions

Terms & Contusions is the gameplay content pack built on Chained Beasts Mod Foundation. The split is currently a **local 0.8.0 build awaiting manual Solo tests**. Read [installation, packages and rollback](../INSTALLATION.md), [the complete catalog](CATALOG.md) and [delivery status](../STAGE7_HANDOFF.md). The last released/manual-tested product remains the [monolithic v0.7.3 release](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.7.3), whose [archived instructions](../history/README-v0.7.3.md) apply to that tag.

## What exists now

Stage 1 moved these four existing additions into `src/TermsAndContusions/`. Stage 2 integrated their registration, state and modifiers with Foundation; Stage 3 adds the twelve four-rank skills and Counterweight Sling:

| Content | ID | Current implemented behavior | Source |
|---|---:|---|---|
| Bulk Up | Skill `11000` | Four ranks: 2x/3x/4x/5x max HP; actual rank changes preserve HP percentage. Shared rank/order; no grant replay. | `BulkUpFeature.cs` |
| Runner's Wraps | Equipment `2499` | Dodge stamina cost multiplied by 0.75 while equipped; price 40. | `Equipment/RunnersWrapsFeature.cs` |
| Leadfoot | Injury `11010` | Effective severity 1/2/3 reduces speed 10/20/30%; potion healing preserved. | `Afflictions/LeadfootFeature.cs` |
| Adrenal Graft | Surgery `11020` | Stamina regeneration multiplied by 1.25 for the run; price 0; confirmed native completion/history. | `Surgeries/SurgeriesFeature.cs` |

- Stage 6 replaces Adrenal Graft's legacy heuristic with shared confirmed native completion and native history; see [surgeries](STAGE6_SURGERIES.md).

## Complete target catalog

The machine-readable [manifest](manifest.json) tracks **all 46 required entries**, including their stable namespaced key, existing numeric ID where applicable, current source/status, rank/severity status, persistence status, completion stage, and manual-verification status. Shared registration now rejects key/category-ID/native/legacy-slot collisions. New numeric IDs remain unallocated until their implementation stage verifies each category's native constraints; keys and existing IDs stay stable.

| Category | Implemented code | Still planned | Target |
|---|---:|---:|---:|
| Skills | 12 | 0 | 12 |
| Equipment | 12 | 0 | 12 |
| Potion afflictions | 11 | 0 | 11 |
| Surgeries | 11 | 0 | 11 |
| **Total** | **46** | **0** | **46** |

**Skills, Stage 3 — implemented, runtime unverified:** Bulk Up's four-rank expansion; Measured Violence, Running on Fumes, Catch Your Breath, Expensive Taste, Fresh Meat, Pick Your Moment, Mixed Martial Arts, Payback Plan, Waste Not, Breathing Room, Longshot.

**Equipment, Stages 3–4 — implemented, runtime unverified:** Counterweight Sling (2498, price 45) and Runner's Wraps (2499, price 40) are preserved. Stage 4 implements Executioner's Hood, Lead-Lined Purse, Borrower's Scabbard, Butcher's Apron, Last-Gasp Locket, Mercenary's Ledger, Pickpocket's Mitts, Chalk Bag, Salvager's Hook, and Duelist's Spurs (2488–2497). See [prices, mechanics, configuration and manual checks](STAGE4_EQUIPMENT.md).

**Afflictions, Stage 5 — implemented, runtime unverified:** preserved Leadfoot plus Lead Belly, Brittle Guard, Shaky Resolve, Tin Ear, Burning Lungs, Heavy Landing, Loose Change, Cold Start, Obsessive Focus and Overheating. Native effective severity, potion healing/acquisition/cure paths, pending state and remaining-time persistence are covered in [the affliction guide](STAGE5_AFFLICTIONS.md).

**Surgeries, Stage 6 — implemented, runtime unverified:** preserved Adrenal Graft plus Anchor Heels, Spring-Loaded Knees, Dense Skeleton, Auxiliary Tendons, Marrow Enrichment, Pain Converter, Emergency Clotting, Counterweight Implant, Fury Pacemaker and Retractable Talons. See [actual mechanics, prices, shared offers/completion and manual checks](STAGE6_SURGERIES.md).

Recall Ring, Debt Collector, Bowling for Beasts, and Blood Bank Flask are excluded. Exact formulas, timing, exceptions, and interactions remain in the [full specification](../EXPANSION_SPEC.md). All 46 catalog entries have mechanics; the new runtime behavior remains manually unverified.

## Build and installation

```powershell
pwsh -NoProfile -File .\scripts\build-plugins.ps1
```

This builds Foundation first and then the pack. `-GameRoot`/`CHAINED_BEASTS_GAME_DIR` select the existing game/loader references. Pack identity is assembly `TermsAndContusions`, namespace `TermsAndContusions`, GUID `local.chainedbeasts.termsandcontusions`, and dependency Foundation `>=0.8.0 <0.9.0`.

The split installation contains both intended production DLLs, with the new Foundation replacing the old content-bearing DLL exactly once. Foundation-only users will need just the Foundation DLL. [Missing-content and matching-save policy](../foundation/PERSISTENCE.md) is now implemented in source. Stage 7 provides separate local ZIPs, explicit closed-game deployment, complete backups and paired rollback; build/check commands still never install.

The [standalone skill example](../../examples/StandaloneContentExample/README.md) builds separately and is excluded from normal deployment. No game, loader, Unity, interop, asset, or save files are distributed. Source and documentation remain MIT.

## Configuration

The pack's eventual configuration is `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`. Its first-load migration imports the eight legacy content enable/force booleans only where the destination has no explicit value. Force defaults remain false for fresh users; intentionally enabled existing test overrides are preserved. Foundation keeps diagnostics/menu settings and its original configuration path. See the [migration guide](../foundation/CONFIGURATION_MIGRATION.md).

English and the other eleven previously supplied language choices are retained, with existing I2 keys and fallback hooks. Their translations are not reviewed by native speakers. Stage 3 supplies current/next four-rank descriptions. New skill/sling/dynamic rank text uses English fallback; the existing twelve-language aliases are retained and Bulk Up's old descriptions now state 2/3/4/5x cumulative HP.

## Verification and known limitations

- Built DLLs and nongame checks are not gameplay verification. Dependency ordering, configuration import in the actual chainloader, offer presentation, all 46 entries and their acquisitions/effects, developer-tool removal, and save/restore after the split need later manual checks.
- Shared persistence, one-time import, canonical removal/reacquisition and exact checkpoint association are implemented and checked as production logic. Actual native callbacks/write/restore and HP/HUD behavior await manual testing. Back up native saves and sidecars as one set; unavailable owned content blocks run restoration.
- The optional menu and rerolls remain in Foundation. Skill/potion rerolls were manually confirmed in 0.7.3. Surgery rerolls animate but kept every option unchanged in the user's test; the issue is deferred. Potion card replacement can leave captured DOTween animation warnings.
- Developer actions and forced offers now require real Solo mode, one local client/player and host authority; temporary cheats clear on reset/leaving that context. Multiplayer support is not claimed.
- Existing sprites/models are reused from the installed game; original icon import is not implemented. Existing screenshots are retained on the root page and labeled as the earlier build.
- Content balance is experimental. All 46 entries have implemented code: twelve skills, twelve equipment items, eleven afflictions and eleven surgeries; runtime unverified.

See [skills/throwing](STAGE3_SKILLS.md), [equipment](STAGE4_EQUIPMENT.md), [afflictions](STAGE5_AFFLICTIONS.md) and [surgeries](STAGE6_SURGERIES.md). Build/check with `pwsh -NoProfile -File .\scripts\test-stage7.ps1`. The game has not run these builds. New equipment/affliction/surgery descriptions use English fallback; native card fit, physical transfer, healing, severity/suppression, shield and crowd behavior remain manual checks.

Follow [expansion progress](../EXPANSION_PROGRESS.md) for the next stage. This checkout is not a new published release.
