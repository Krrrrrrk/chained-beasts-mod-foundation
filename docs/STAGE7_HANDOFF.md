# Stage 7 integration handoff — 0.8.0

Updated 2026-10-26 (America/New_York). Local experimental work; **runtime unverified and unpublished**. All implementation stages are complete; final build, package and installed hashes and the exact rollback checkpoint are recorded below. No game launch or live gameplay testing is authorized or performed.

> Historical 0.8.0 delivery record: the user subsequently reported a gameplay crash. The [0.8.1 crash hotfix report](CRASH_2026-10-26.md) records current installation, defects, backups and pending retest. The hashes below remain historical.

> Current follow-up: [0.8.2 second crash/presentation hotfix](CRASH_2026-10-26_SECOND.md). Both earlier runtime tests crashed; current runtime success remains pending.

## Reconciliation

Stages 1–6's uncommitted work was preserved. The complete specification remains unchanged. All **46 entries** now have code and map through the [manifest](terms-and-contusions/manifest.json) and [generated catalog](terms-and-contusions/CATALOG.md) to category/ID/key, effect/scaling, price/config, registration, source/state, removal and an individual [manual test](MANUAL_TESTS.md). There are 12 four-rank skills, 12 equipment, 11 three-severity afflictions and 11 once-per-run surgeries; no deferred idea or placeholder is counted. Original four IDs, GUIDs, net6.0 target and working BepInEx version remain intact.

Stage 7 adds compiled end-to-end category examples/tutorials, independent-plugin setup, API/composition/throw/localization guides, installation/package/migration/rollback instructions, full per-entry and interaction checks, regenerated honest manifest status and local-only delivery scripts. Foundation can load without the pack. Examples are excluded from all product ZIPs and normal deployment. Existing screenshots remain historical, original icons are reused, balance is experimental and MIT/source/dependency notices are retained.

Two concrete integration issues were resolved: Waste Not/Locket now use the shared one-time owner/clamped stamina API instead of passive-regeneration context; Pick Your Moment's attempt-wide bonus now uses shared native AttackDamage attribution so area attacks don't silently lose it, and direct contacts can't multiply it twice. F1 gains a custom-only skill/injury filter and explicit rank/severity labels, with unchanged cheat defaults and Solo authority.

## Verification and delivery

See the final recorded results below. Builds/metadata/pure-state and synthetic deployment/rollback/archive tests do not prove actual chainloader, IL2CPP detours or gameplay behavior. No live purchase/grant functions are invoked by these checks. Package staging, binaries, backups, generated bindings, private data and raw logs remain ignored and uncommitted.

## Remaining limitations and next action

- **All new native startup, UI, mechanics, HP/stamina/HUD authority, physics, save/restore and generic Fury interception await the user's manual Solo checks.** Last manually tested public version remains v0.7.3; no 0.8.0 runtime log is claimed.
- Solo only; host status alone is insufficient. No multiplayer verification or automatic Steam Cloud sidecar synchronization. Restore matching native SaveData and sidecar archive as one full set.
- Native checkpoints omit complete arena instances and current stamina/recovery timers. Live matching objects can retain tokens in process; newly reconstructed/despawned objects cannot be treated as the old physical spawn. Vanished focus/bleed targets are explicitly cleared; this isn't full arena persistence.
- Native-build-specific coroutine/method bindings and highest-speed throw collisions/preview behavior require real testing. Ordinary ThrowWeapon weapons/objects/limbs are supported; independent bow firing/offhand/grapple/generated launches don't gain normal-throw range/charge bonuses.
- New English fallback and unreviewed older twelve-language aliases; no original-icon import workflow. Small integer timed heals may round differently from one combined heal.
- Surgery rerolls remain deferred; deterministic same-visit sampling doesn't promise different offers. Earlier native animation warnings may persist; preserve fresh logs and distinguish new failures.

**Next is manual Solo verification, not another automatically started implementation stage.** Follow [the checklist](MANUAL_TESTS.md), capture fresh logs/screenshots manually, then address evidenced failures. No GitHub push, release publication or Thunderstore upload was performed. Downgrade only with the [matching whole checkpoint](INSTALLATION.md), never a DLL alone.


## Final verified results

- Both production plugins and both optional example assemblies: Release/net6.0, 0 warnings / 0 errors.
- Production logic/metadata suites: 50 + 111 + 521 + 191 + 271 + 321 + 169 = **1,634 checks**; **34 synthetic workflow checks**, plus the explicit deployment-refusal guard. Total **1,668 nongame checks**, plus that guard.
- Synthetic workflow covered renamed/nested old DLL replacement, unrelated-plugin preservation, injected mid-copy failure recovery, damaged-backup refusal, whole paired rollback, root traversal/overlap refusal and all ZIP member allowlists/hashes. No game assembly was initialized and no live grant/purchase/gameplay handler was invoked.
- Actual closed-game deployment installed only Foundation then TermsAndContusions, removed the old monolithic DLL, and verified their hashes. Examples and reference DLLs are absent from the installed plugin folder. No game launch, configuration migration or save editing occurred.

### Built and installed DLL hashes

| Assembly | SHA-256 | Installed? |
|---|---|---|
| ChainedBeasts.ModFoundation.dll | `93fbd8ea639f6a0f29ab906cbf21e84d62d896583225bd3d016ead7f7fad163d` | Yes |
| TermsAndContusions.dll | `2909ac510ffbfcb1a1ef1a52d2d0d7b5117c2727da7a73a26673927077e189e5` | Yes |
| StandaloneContentExample.dll | `074ce57bcacc545ba457f8a43d65127e8fa5e38108f4a01c58fa44372830278b` | No; optional example |
| CategoryExamples.dll | `0996952b0f2c6535e3e08cfe206f2939e3c927d46a924718a8b4a7a6396ee7d2` | No; optional example |

### Local package hashes

| Package | SHA-256 |
|---|---|
| ChainedBeasts.ModFoundation-0.8.0.zip | `2c15517a6a14712b589569c8a2edbc2e02076e5e93d31b29b9576df784be3e28` |
| TermsAndContusions-0.8.0.zip | `12501aab386cc191bcb800bf92fa54b50f1a6c2606950ba33d64fc39f16e76af` |
| TermsAndContusions-with-Foundation-0.8.0.zip | `c0bf835761ede7aa3a245af0da8ff4b4a85b693479ecdf93a378cde57329bd4d` |

### Exact rollback checkpoints

- Before Stage 7 source changes: `backups/expansion-stage7-20261026-061950`.
- Immediately before actual deployment: `backups/expansion-stage7-deploy-20261026-065301-863`.
- The deployment checkpoint contains source/builds, original installed v0.7.3, both existing configs, all 23 user-data files, a verified SHA-256 inventory and matching-root checkpoint metadata. Generated test checkpoints are separate and cannot restore against mismatched real roots.
- Roll back with `scripts/restore-plugin-backup.ps1 -Restore -BackupRoot <the exact deployment checkpoint above>` and the matching GameRoot/UserDataRoot. It restores the entire saved plugin/config/user-data set and first saves the current matching set. The old v0.7.3 must never resume a new-protocol save without its pre-expansion user-data restore.
- Package/installed checksums and the complete rollback record are also in ignored `artifacts/packages/0.8.0/{packages,deployment}.json`. No GitHub/Thunderstore publication occurred.

### Final integrity audit

All 25 pre-existing config/user-data files, five screenshots, four protected original game binaries and the byte-identical full specification are unchanged. Installed inventory is exactly two production DLLs, two existing config files and 23 user-data files; the game remains closed. All three final ZIP hashes and member hashes match their manifests and deployed payloads. All 289 current local documentation links resolve; whitespace checks pass and no unignored binary/archive/raw-log artifacts entered the source change set. The final manifest/tutorial/catalog metadata audit passed again after recording delivery. No runtime test or publication is implied.
