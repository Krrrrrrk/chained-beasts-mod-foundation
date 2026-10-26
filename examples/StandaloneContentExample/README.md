# Standalone Content Example

Minimal independent plugin using the public Foundation API. It supplies **Training Footwork**: one normal skill rank, 5% lower dodge stamina cost while owned. It reuses the native dodge skill sprite. It is a teaching example outside the 46-entry Terms & Contusions catalog, not part of the production deployment or packages. Runtime behavior remains untested.

| Identity | Value |
|---|---|
| Assembly / namespace | `StandaloneContentExample` |
| Plugin GUID / dependency | `example.chainedbeasts.standalonecontent` / Foundation `>=0.8.0 <0.9.0` |
| Stable content key | `example.standalone:skill/training-footwork` |
| Skill ID / maximum rank | `12000` / `1` |
| Localization | Namespaced Name/Description terms, explicit English fallback |

Build from the repository root with the same prerequisites and installed game bindings as the production plugins:

```powershell
dotnet build .\examples\StandaloneContentExample\StandaloneContentExample.csproj -c Release
# Optional non-default installation:
dotnet build .\examples\StandaloneContentExample\StandaloneContentExample.csproj -c Release '-p:ChainedBeastsGameDir=D:\Games\Chained Beasts'
```

Output: `examples/StandaloneContentExample/bin/Release/net6.0/StandaloneContentExample.dll`. Reference DLLs and Foundation are not copied into the output. It remains excluded from normal product packages/deployment; manually install only after the full matching backup.

`Plugin.cs` claims a `ContentDescriptor`, registers a native SkillDef factory/verification callback, registers owned terms through `FoundationText`, routes the three native skill-name/description paths to those terms, and registers a stat contribution. Foundation's shared catalog exposes it to the native normal skill pool. No free grants, replaced vanilla skills, new assets, per-frame stat writes, or forced offers are needed. Native eligibility and acquisition bookkeeping remain in charge. Native Add/RemoveSkill changes its effective modifier; the shared sidecar captures/restores rank and acquisition order without a new reserved slot or acquisition replay.

For a separate mod, change the plugin GUID, assembly/namespace/project name, content key, numeric ID, definition name, term suffixes/text, and effect. Keep the compatible Foundation dependency. Never copy a `LegacyMarker` declaration: those are imports for specifically identified old save formats. Keep IDs explicit and stable; collisions fail rather than renumbering old saves. Factory failures disable the identity; installation failures unpatch this plugin's hooks and disable its contributions.

The example's strings use English fallback. `FoundationText.Register` optionally accepts translations keyed by the game's I2 language names. Terms & Contusions retains its existing separate 12-language table and aliases. New art/icon import is not implemented.

The [persistence guide](../../docs/foundation/PERSISTENCE.md) includes a combined Bulk Up + Training Footwork save/restore/removal test. Manually install only the example DLL alongside a compatible Foundation, then acquire it through a normal trainer or the generic solo developer tool. Remove it only after finishing/abandoning its run or restoring the matching pre-example backup; resuming a checkpoint that owns a missing example is intentionally blocked. Verify your sample manually before using it in an important run.

This sample is embedded with its complete source in the [skill tutorial](../../docs/foundation/tutorials/SKILL.md). See [separate mod setup](../../docs/foundation/SEPARATE_MOD.md), [installation/rollback](../../docs/INSTALLATION.md) and [additional category examples](../CategoryExamples/README.md). All new native runtime behavior remains unverified.
