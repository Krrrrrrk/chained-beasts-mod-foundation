# Terms & Contusions 0.8.3

## Changes

- All pack-owned descriptions start with `TC: `, including skill offers/owned ranks, equipment, potion severities, surgery text and the four legacy examples' translated descriptions. Names, IDs, eligibility, costs and mechanics stay unchanged. The marker is applied once, even through layered localization lookups. Vanilla strings and independent Foundation plugins are not tagged.
- The four original items use their normal pools. New `[DeveloperOffers] EnableForcedOffers` defaults to false, so old migrated Force flags cannot silently prioritize Bulk Up, Runner's Wraps, Leadfoot or Adrenal Graft. Deliberate testing requires the master switch plus the relevant Force flag. Existing offers already stored in a run are not rewritten.
- The main README is written in the project author's voice, with the normal-offer policy, `TC:` marker, two-plugin installation and build instructions. The unbalanced examples/custom-icon limitations remain explicit.

## Verification status

The author reported a crash-free **0.8.2** gameplay test. That is encouraging manual evidence for the previous hotfix, not exhaustive verification of every rank, interaction or checkpoint. **0.8.3 still needs a fresh in-game check** of the tags and normal offer distribution. No game launch, menu automation, live grants or gameplay simulation is part of this update.

Four projects built Release/net6.0 with zero warnings/errors. **1,774 nongame checks pass** (50/111/521/191/271/321/275 stage checks plus 34 synthetic workflow checks), along with the explicit deployment-refusal guard. Prefix checks exercise every generated skill rank, affliction severity, equipment/surgery description and idempotent layered tagging. Existing 125-character limits still include the marker. These checks do not initialize native gameplay.

Both **0.8.3 DLLs are locally deployed**, with matching build/installed/ZIP hashes:

| Assembly | SHA-256 |
|---|---|
| ChainedBeasts.ModFoundation | `ef6d3c4a04508f65dab732c66fa865c584151446536d2e83044e2d7f2370ecce` |
| TermsAndContusions | `bb958bc7660cf3128c376eba59b0c498ca6edf6815344f4b2db1d3946cac263a` |

The local package/deployment records are under `artifacts/packages/0.8.3/`. Combined ZIP SHA-256: `0f61021f73ec46ad8b318cfd5813e2e149aba7f371eccb19e604145df1eb3712`. All **27 current user-data files** are byte-identical to the pre-change checkpoint. Exactly the four requested Force values plus the new false developer master differ in the pack config; other configs, feature enables, game binary and immutable original specification remain unchanged. Three config files match the immediate pre-deploy snapshot. No native saves or shipped assets were edited.

The pre-change source/DLL/config/user-data checkpoint is `backups/expansion-stage7-community-update-20261026-083449-220`. The immediate pre-deploy matched checkpoint is `backups/expansion-stage7-deploy-20261026-084610-068` (0.8.2 DLLs with normal-offer config). For deliberate paired rollback with the game closed, use `scripts/restore-plugin-backup.ps1 -Restore -BackupRoot <matching checkpoint>`; preserve newer run progress first because it restores user data as well. Source restoration is separate. No rollback was performed.

Source publication targets [the repository's main branch](https://github.com/Krrrrrrk/chained-beasts-mod-foundation) with explicit authorization. The existing remote README edit is retained in history and its first-person intent is incorporated into the rewrite. Compiled packages stay outside Git; source publication does not itself create a GitHub Release or upload ZIP assets.

## Configuration

In `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`, keep `[DeveloperOffers] EnableForcedOffers = false` for normal play. The four Force flags can remain available for testing, but are ineffective without that switch. Restart to apply settings. No content is removed from existing ownership.

The local update explicitly turns the four old Force settings off and writes the master switch false. Other configuration settings and game user data are preserved. Backups are outside BepInEx scanning paths and excluded from Git.

## GitHub Release

[0.8.3 is published](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.8.3). It includes all three plugin ZIPs, both raw DLLs and SHA256SUMS.txt. Every public asset was downloaded and hashed against the prepared build; all six match. Recommended: the combined pack/Foundation ZIP. The release tag points to source commit `4d7d62ae9815661307dbfe31e11615ae46b4882b`; the README removes the requested legacy-item paragraph and links the exact official tested BepInEx download. No plugin code or deployed DLL changed during this documentation/publication step.

The package manifest inside each ZIP records its original build-time local state; the release record here and current content manifest record publication. Publication does not set runtimeVerified or pass pending manual gameplay cases.
