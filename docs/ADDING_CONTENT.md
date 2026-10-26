# Adding content with Foundation 0.8.0

Read [separate mod setup](foundation/SEPARATE_MOD.md) first: change GUID/assembly/namespace/content keys/IDs, retain the hard version dependency and use installed generated runtime bindings. Then choose an end-to-end tutorial with complete compiled source:

- [Skill: Training Footwork](foundation/tutorials/SKILL.md) — normal SkillDef/ownership, view terms, rank/order persistence and removal; four-rank pack extension explained.
- [Equipment: Practice Wraps](foundation/tutorials/EQUIPMENT.md) — native PurchaseDef/price/shop/equipped history, current-equipped factor and limited-ledger pitfalls.
- [Potion affliction: Practice Limp](foundation/tutorials/AFFLICTION.md) — normal InjuryDef/potion healing, effective severity, suppression/cure and independent state.
- [Surgery: Practice Graft](foundation/tutorials/SURGERY.md) — shared offers, genuine completion, native payment/history and once-per-run ownership.

The standalone skill and category examples build independently against the actual public API; neither is deployed or packaged with the 46-entry pack. [Composition/events](foundation/MECHANICS.md), [localization/visuals](foundation/LOCALIZATION_VISUALS.md), [checkpoint state](foundation/PERSISTENCE.md) and [configuration migration](foundation/CONFIGURATION_MIGRATION.md) explain advanced cases. The [complete catalog](terms-and-contusions/CATALOG.md) and [manifest](terms-and-contusions/manifest.json) map every production entry to source and manual checks.

A new skill's ID does not create a new private save slot. Never copy Bulk Up's old None or Leadfoot's old EmptySlot1 marker: these were specific legacy imports, not a new-feature storage API. New skills/afflictions receive independent namespaced ownership records, native equipment/surgery histories remain authoritative, and timer/allowance ledgers need their own versioned key. Restore only exact matching native-save/sidecar sets.

`RunDataManager.GetTrainingSkills` is the inspected training hook; there is no SkillGenerator.GetTrainingSkills contract. No GameLibrary surgery registry exists. All category differences are deliberate. Current runtime behavior remains awaiting manual testing; see [installation](INSTALLATION.md) and [manual matrix](MANUAL_TESTS.md). Original icons are reused, original-asset import is unimplemented and these unbalanced examples are not gameplay recommendations.

The [archived v0.7.3 guide](history/ADDING_CONTENT-v0.7.3.md) documents the old monolithic version only. The full original requirements remain in [EXPANSION_SPEC.md](EXPANSION_SPEC.md).
