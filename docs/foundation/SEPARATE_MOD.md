# Create a separate Foundation mod

Start from the [standalone skill project](../../examples/StandaloneContentExample/StandaloneContentExample.csproj), or the [three-category teaching project](../../examples/CategoryExamples/CategoryExamples.csproj). Both compile against the actual Foundation project and installed runtime bindings. Neither references TermsAndContusions or ships in its packages. Build success does not verify native runtime behavior.

## Names and identities before copying

| Identity | Standalone skill sample | Change for your own plugin |
|---|---|---|
| Project/assembly/namespace | `StandaloneContentExample` | A unique product name; update csproj, namespace and output filename together |
| BepInEx GUID | `example.chainedbeasts.standalonecontent` | Your own stable reverse-domain identity; independent config and Harmony owner |
| Display name/version | Standalone Content Example / 0.8.0 | Your own name/version, Foundation hard dependency `>=1.0.0 <2.0.0` for the current source |
| Content key | `example.standalone:skill/training-footwork` | Your own namespace/category/slug; independent ownership and text |
| Skill ID | `12000` | Verify a vacant installed category ID and coordinate with other mods |
| Other sample IDs | equipment `2470`, injury `12010`, surgery `12020` | Demonstrations verified vacant in this build, not a global reservation service |
| Localization suffixes | `/Name`, `/Description` | Terms derive from your content key; provide translations or explicit English fallback |
| Feature state | None needed for these stat-only examples | Use a separate namespaced/versioned JSON key for each ledger you add |

Never change the existing pack IDs to make room for a copy. Adding content **inside** the pack means adding a new definition/constant/key while preserving existing IDs and config keys. For a separate plugin, change GUID, assembly, namespace, content and localization identities together. Foundation rejects duplicate keys/category-IDs/native-enum claims; it does not silently renumber them. Disabled claims still reserve their identities. A public registry isn't a global discovery service for every unrelated mod.

Copy the selected complete project into your workspace, adjust its ProjectReference/Import relative paths or reference the built Foundation DLL with `Private=false`. `src/Directory.Build.props` shows installed BepInEx/Unity/game references; set `ChainedBeastsGameDir` or `CHAINED_BEASTS_GAME_DIR` instead of hardcoding another user's path. Every game/runtime reference stays `Private=false`. The skill csproj is complete; the category project includes `Plugin.cs` plus all three feature files. To extract one category, retain only that category's claim and Install call in the loader and remove the other two files. Do not copy a feature file alone and expect it to have a plugin entry point.

## Load order and native registration

`BasePlugin.Load()` checks `FoundationServices.IsReady`, claims descriptors, registers owned text/modifiers and schedules factories through `RegisterLibraryCallbacks`. The factory runs **before GameLibrary.Init builds dictionaries**; the verification callback runs afterward. Do not mutate a native library that has already completed startup or claim a public method exists without inspecting the generated interop. The installed source uses exact `FeaturePatcher.RequireMethod` parameter types.

Each factory checks the live list for foreign collisions and tracks only its own successful addition. Copy **only sprite/model references** from a native donor; create a fresh SkillDef/PurchaseDef/InjuryDef and new identity. Cloning a donor's gameplay fields can accidentally preserve its effect, cost or eligibility. Surgeries instead use `SurgeryServices.Register`: there is no GameLibrary surgery registry. Its shared adapter performs eligibility, payment/completion confirmation and history ownership.

The sample loader owns its Harmony GUID, unpatches on installation failure and disables its claims. Foundation stops their library/text/modifier callbacks. Your advisory event handlers must also check their enabled/owned state. Native restore refuses unavailable owned content. Do not treat a caught installation exception as success.

## Build, install, coexist and package

```powershell
$env:CHAINED_BEASTS_GAME_DIR = 'D:\Games\Chained Beasts'
dotnet build .\examples\StandaloneContentExample\StandaloneContentExample.csproj -c Release
dotnet build .\examples\CategoryExamples\CategoryExamples.csproj -c Release
```

Build Foundation first or let the ProjectReference do it. To manually test an optional example, close the game, back up the full DLL/config/user-data set, and copy **only the example DLL** into BepInEx/plugins beside matching Foundation. Terms & Contusions is optional. Never copy the example's reference DLLs, a second Foundation build or all bin files. The normal product deploy script intentionally doesn't install either example.

Check the fresh startup log for dependency/registration failures and native library indexing. Then acquire the example normally, verify its targeted views/effect, save/resume together with original Bulk Up/Leadfoot and another copied feature, remove/cure/unequip independently and start a fresh run. Do not use a dev grant as proof of normal offer/cost behavior. Package your own DLL under `BepInEx/plugins/`, install README/license/notices and an explicit Foundation version requirement; distribute no loader, game/runtime bindings, saves or extracted assets.

Continue with the complete category tutorials: [skill](tutorials/SKILL.md), [equipment](tutorials/EQUIPMENT.md), [affliction](tutorials/AFFLICTION.md), [surgery](tutorials/SURGERY.md). For advanced mechanics use [API/interaction boundaries](MECHANICS.md), [localization/visuals](LOCALIZATION_VISUALS.md) and [checkpoint persistence](PERSISTENCE.md). Never copy the old reserved-slot bridges: changing a content ID does not change a hardcoded native save slot, so two copies would overwrite each other's state.
