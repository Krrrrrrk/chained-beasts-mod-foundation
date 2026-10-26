# Foundation and pack ownership

## Dependency direction

```mermaid
flowchart LR
    Pack[TermsAndContusions.dll] -->|hard dependency >=0.8.0 and <0.9.0| Foundation[ChainedBeasts.ModFoundation.dll]
    Example[Optional StandaloneContentExample.dll] -->|same version range| Foundation
    Pack --> Game[Installed generated IL2CPP bindings]
    Foundation --> Game
    Example --> Game
```

Foundation has no pack/example dependency. All plugins retain `net6.0`, tested loader references, unique GUIDs and assemblies. The example is outside the production catalog and deployment.

## Ownership

| Foundation | Terms & Contusions |
|---|---|
| Shared registration, numeric/key/native collision checks | Four existing numeric IDs and stable pack keys |
| Library callback scheduling and native catalog enumeration | Actual SkillDef/PurchaseDef/InjuryDef factories and eligibility |
| Owned-text API for independent plugins | Preserved twelve-language table and numeric/legacy aliases |
| One stat/modifier dispatch point per existing stat | Bulk Up, Runner's Wraps, Leadfoot and Adrenal Graft factor rules |
| Canonical ownership, run/battle/equipment events | Bulk Up proportional current-HP transition subscriber |
| Versioned checkpoint capture/matching/write/restore | Two declarative, example-specific legacy import rules |
| Generic developer tools with actual Solo authority | Four startup enable choices and Solo-only forced offers |
| Exact-signature patch helpers and config import helper | Native surgery generator/completion compatibility implementation |

The shared core owns canonical skill/affliction state and stat dispatch. The pack retains its original numeric identities and uses normal ownership events; the old menu-only notification remains for API compatibility.

## Startup, collisions and cleanup

BepInEx's hard dependency orders Foundation first. Foundation installs its checkpoint, ownership, stat and text adapters as one Harmony owner and sets `IsReady` only after success. Failure unpatches that owner's partial hooks. The pack/example refuse to initialize without it.

The pack binds its startup booleans, intentionally imports existing config choices once, and atomically claims all owned identities even when disabled. A later batch collision leaves none of its earlier claims installed. Enabled factories run around native GameLibrary.Init; shared GetAllSkill/Purchase/InjuryTypes hooks expose registered content without duplicate IDs. Surgeries explicitly use their generator rather than pretending a registry exists.

Duplicate keys, category IDs, native IDs and legacy-slot claims fail. A factory verifies foreign definitions before adding its own; verification/localization failures disable that identity and contributions. Per-feature installation failures unpatch that feature and disable its identity. Disabled identities do not retain active pack lookup aliases or stat rules. No automatic renumbering can invalidate an old save silently.

The optional standalone plugin demonstrates real public API use and its own failure cleanup. Its per-skill rank/order is stored by Foundation, without a copied marker. Shared source registries cannot police arbitrary foreign mods that bypass the API or mutate definitions after initialization; that compatibility remains outside the guarantee.

## Ownership, stats and presentation

Canonical Add/RemoveSkill and Add/RemoveInjury hooks snapshot the before rank and publish only a successful changed rank. Signed native counts are bounded; nested calls and duplicate finalization do not emit another event. Pointer-based keys exist only inside the synchronous mutation guard, never saved. Checkpoint restore assigns dictionaries rather than invoking acquisition.

Bulk Up now uses factors 2x/3x/4x/5x at ranks 1–4. Both gain and loss scale current HP by new max / old max with attainable-health/death clamps; an ordinary removal followed by reacquisition has a new real before/after transition. The developer menu avoids repeating its generic HP adjustment for registered custom skills. The four-rank formula is tested through the same production logic; native HUD behavior remains manual.

The shared pipeline recomputes contributions from the otherwise-normal getter value. No cached maximum is multiplied on refresh, and reads never heal. Leadfoot reads native effective severity, respecting suppression/reduction; equipment predicates reflect actual current equipment; surgery history remains native authoritative.

The confirmed v0.7.3 banner fix remains intact: forced Bulk Up updates both the stored training row and the temporary return list. Acquisition and later reveal see the same ID even after ownership stops forcing new offers. No native banner detours, arbitrary UI scans or global string replacement were added.

## Persistence and supported scope

See [the exact protocol](PERSISTENCE.md) and [native hook safety](MECHANICS.md#native-hook-safety). New per-content ownership is external, associated with one shared native discriminator and canonical full-checkpoint fingerprint. A sidecar is published only after successful native SaveData task completion, with exact-file proof for crash-window recovery. Native equipment/surgery histories are never overwritten by duplicate sidecar ownership.

Run reset/clear/new identity clears live state. Immutable archives remain for paired older-save backups. Unknown data is preserved; actual restoration refuses missing/incompatible/disabled owned content rather than resuming an incomplete build. The adapter currently supports Solo on the inspected Windows Steam/default SKU; multiplayer persistence is not implemented.

Developer actions and forced offers require real Solo mode, one local client, one player, and host authority. God mode/stamina overrides clear on reset or leaving that state. The minimal battle key covers the current level scope, not all future enemy/weapon lifecycle semantics.

The runtime exposes scoped committed attacks, HP-loss receipts, physical weapon identity, execution/backstab/recovery events, stamina/shield/dodge/crowd events and native surgery completion. The pack owns the formulas, prices, severity and timed allowances. Feature-state validation runs before restore, and capture failures preserve the prior native save. Native surgery history remains authoritative. See [mechanics and events](MECHANICS.md).
