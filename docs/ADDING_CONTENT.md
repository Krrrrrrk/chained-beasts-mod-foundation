# Adding custom content

This guide describes the implemented examples, not a generic asset-loader API. Chained Beasts has four different native content paths, so the source intentionally preserves their differences instead of forcing them behind one abstraction.

## Start with identities and compatibility

All owned IDs live in `CustomContentIds.cs`:

| Enum space | Example | ID | Tested native range observation |
|---|---|---:|---|
| `SkillType` | Bulk Up | `11000` | Kept unchanged from the working earlier implementation. |
| `PurchaseType` | Runner's Wraps | `2499` | Undefined in the tested enum/catalog, but inside the game's equipment classification range `2000..2500`. |
| `InjuryType` | Leadfoot | `11010` | Above the observed native/reserved injury values. |
| `SurgeryType` | Adrenal Graft | `11020` | Above the observed native surgery values. |

These are separate enum spaces; equal numbers in different spaces would not identify the same kind of content. Every installer also calls `Enum.IsDefined` and registration checks fail closed if a game update claims its ID. Before choosing another ID, inspect all defined, WIP, abandoned, and reserved catalog rows—not just currently offered content—and retain a runtime collision guard.

The prototype visuals are references to assets already loaded from the user's game. Do not extract or distribute them. A copied definition must receive a new enum identity; copying an original identity can silently inherit native behavior.

## Skill example: Bulk Up

Copy `BulkUpFeature.cs` when the new idea is fundamentally a player skill.

1. Add a new `SkillType` value in `CustomContentIds.cs`. Do not change Bulk Up's `11000`.
2. Change the internal definition name, display text, `SkillDef` fields, rank count, and the native skill chosen only as the sprite source.
3. Register before `GameLibrary.Init`, verify the resulting `SkillDict`, and append the custom enum to `GameLibrary.GetAllSkillTypes`.
4. Put ordinary trainer-offer behavior in the `SkillGenerator.GetTrainingSkills` postfix. Keep any force option as a one-slot developer override rather than a grant.
5. Patch the narrow property or acquisition call that owns the effect. Bulk Up patches the final health paths and snapshots `RunDataManager.AddPlayerSkillRPC` so the health change occurs once after a successful native acquisition.
6. Bulk Up stores a compact marker in the fixed-size checkpoint data because custom `SkillType` keys are not part of the game's native enum-sized skill array. Its run reset hooks clear transient bookkeeping.

Manual check: view the name/description, acquire it through a trainer, compare current/max health, checkpoint/restore the run, and start a fresh run. Confirm other skill offers and ranks are unchanged.

Definitions control identity, availability, ranks, icon references, and tooltip metadata. Health behavior still requires C# because a new enum value has no native switch case.

## Equipment example: Runner's Wraps

Copy `Equipment/RunnersWrapsFeature.cs` for a purchasable equipment item.

1. Choose an unused `PurchaseType` that the game's `GladiatorConsts.IsEquipment` recognizes. Change `RunnersWrapsPurchase` and keep the collision guards.
2. Change `DisplayName`, `Description`, `Price`, the internal definition name, and the selected asset-source equipment.
3. `GameLibraryInitPrefix` creates a new `PurchaseDef` before the native dictionaries are built. It copies only the `modelPrefab` and `sprite` references from Running Shoes; category, cost, release state, and identity are set independently. `GetAllPurchaseTypesPostfix` makes the ID visible to normal purchase generation.
4. `StringManager.GetPurchaseName/GetPurchaseDesc` cover generated tooltips. `CustomLocalization` also handles the numeric `Purchases/2499_*` terms through `StringManager.GetLocalisedTerm` and I2's `GetTranslation/TryGetTranslation`, which are used by offer cards and character-sheet panels. Replace the English map with real translation-table integration if shipping multiple languages.
5. `Player.StaminaCostDash` is the effect hook. Its postfix multiplies the already-computed native result by `0.75` only while `Player.HasEquipment(customId)` is true. This preserves skills, injuries, and other modifiers and cannot accumulate during refreshes.
6. No extra save state is needed: the game's normal equipment ownership/equipped-slot data owns the lifecycle. Replacing/removing the item makes `HasEquipment` false immediately.

`GenerateShopOptionsPostfix` is only the developer convenience path. It changes one existing equipment result and leaves the rest of the generated list intact. With the override off, the registered purchase participates through the normal purchase-type pool.

Manual check: buy it for 40, equip it, measure one dodge cost, refresh equipment screens, then replace/remove it and measure again. Confirm max/current stamina never changes.

## Potion affliction example: Leadfoot

Copy `Afflictions/LeadfootFeature.cs` for a lasting, skull-based potion result.

1. Choose an unused `InjuryType`; change `LeadfootInjury`, the definition name, text terms, severity limit, body part, and sprite source.
2. `GameLibraryInitPrefix` adds a real `InjuryDef` before native dictionary construction. `GetAllInjuryTypesPostfix` exposes the new ID to injury generation. The example reuses Broken Foot's sprite reference but none of its identity or code paths.
3. Description terms are severity-specific. The English prototype maps levels 1–3 to 10%, 20%, and 30% slowdowns through all three localization boundaries, including the numeric terms used by character-sheet panels.
4. The normal `InjuryGenerator.GeneratePotions` path supplies healing and potion size. Its developer override replaces one non-water result with a new `Potion` that retains `_healAmount`, `potionSize`, and `modelIdx`; only the injury identity and name term change. Native `ConsumePotionRpc` still heals and calls the ordinary injury application path.
5. `Player.FinalMovementSpeed` is the effect hook. It reads `Player.HasInjury(customId)`, deliberately using the game's effective severity after normal injury suppression/reduction, and multiplies the final native speed once by `0.90`, `0.80`, or `0.70`.
6. Reducing or curing the injury changes the dictionary value used by `HasInjury`; no cached multiplier exists. The checkpoint bridge stores Leadfoot in the unused native `EmptySlot1` array slot while native validation/restore runs, then restores the custom dictionary entry.

Manual check: note health and movement, drink the offered potion, verify the generated healing still applies, then reduce one skull and cure it. Test checkpoint/restore before sharing changes to the marker logic.

The checkpoint bridge is version-sensitive because the game serializes injuries in native-enum-sized arrays. Do not enable another mod that also claims `EmptySlot1`. Do not disable Leadfoot or remove the plugin while an active run contains it; cure it or finish/abandon the run first.

## Surgery example: Adrenal Graft

Copy `Surgeries/AdrenalGraftFeature.cs` for a paid, once-per-run operation.

1. Choose an unused `SurgeryType`; change `AdrenalGraftSurgery`, name, description, price, and category. The example uses `Enhancements` and is currently free (`moneyCost = 0`) to make community testing straightforward.
2. The game has no `GameLibrary` surgery-definition registry. `SurgeryGenerator.GetSurgeries` builds its list monolithically, so the postfix substitutes one returned slot while preserving the option count. Normal mode uses a deterministic one-in-four eligible-visit gate. The developer override uses the first slot, or adds one test slot if the native list is empty. Both paths skip players who already own it.
3. `StringManager.GetSurgeryName/GetSurgeryDesc` supplies the prototype text, with numeric I2 term aliases in `CustomLocalization`. `Surgery` itself carries category, damage, donation/rarity flags, and `moneyCost`.
4. Native `RunDataManager.PerformSurgeryRpc` still selects the option, processes its zero cost, updates ordinary surgery/codex statistics, and drives the surrounding UI. The postfix checks Netcode's RPC execution stage—important because a zero money delta cannot distinguish a sent RPC from an executed one—then adds the custom `SurgeryType` to `PlayerData.previousSurgeries`.
5. `Player.StaminaRecoveryRate` is the effect hook. It multiplies the already-computed recovery rate by `1.25`; maximum stamina and the delay before recovery are untouched.
6. `previousSurgeries` is included in the native player checkpoint as a surgery-enum array, so no supporting skill is required. The generator checks that list to enforce once per player per run. Fresh `PlayerData` starts with an empty list.

Manual check: accept it for free and confirm money is unchanged, exhaust stamina, wait through the unchanged recovery delay, compare the recovery rate, revisit a surgeon, checkpoint/restore, and start a fresh run.

Loading an active custom-surgery checkpoint without the plugin leaves an unknown surgery ID with no effect hook. Re-enable the same plugin/ID to recover the behavior, or finish/abandon the run before removing it.

## Configuration and feature isolation

Add each public feature to `Plugin.Load` with two settings: `Enable...` and an optional `Force...NextOffer`. Shared developer defaults must stay `false`. Pass a unique Harmony suffix to `InstallFeature`; this prevents a failed new feature from unpatching unrelated examples.

Do not use a force option to call purchase, potion, surgery, or skill-grant handlers. A good testing override changes at most one eligible generated slot and lets the player complete the normal flow.

## Before sharing a modification

1. Recheck the installed `GameAssembly.dll` hash and all patch signatures.
2. Re-export or inspect the catalogs, including inactive/reserved rows, and re-run every ID collision check.
3. Build with no warnings and confirm no referenced game/BepInEx DLL was copied into the output or package.
4. Start with force overrides off, then use one at a time for manual testing.
5. Check acquisition, removal/reduction, repeated UI refresh, checkpoint/restore, fresh-run reset, original content, and clean exit.
6. Clearly label any behavior not tested in multiplayer. These examples are scoped and handed off for solo testing.
