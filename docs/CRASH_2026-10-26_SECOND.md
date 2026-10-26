# Second gameplay crash, potion reveal and concise cards — 0.8.2

The user reported another `0xc0000005` gameplay crash after selecting a skill in 0.8.1, a Lead Belly potion reveal showing the previous beast name, and descriptions that were much longer than vanilla cards. The game was closed when inspected. No launch, live gameplay/grant handler, menu automation or screenshots were performed by the agent.

> Subsequent result: the author reported no crashes in the 0.8.2 playtest. Individual catalog cases were not exhaustively verified. [The 0.8.3 community update](COMMUNITY_UPDATE_0.8.3.md) records the next description/normal-offer changes and current installation. The evidence below is historical.

## Evidence and limits

The complete pre-fix checkpoint is `backups/expansion-stage7-crash2-20261026-074531-611`: source/builds, deployed 0.8.1 DLLs, configuration and current full user data. `raw-logs/LogOutput.log` and `raw-logs/Player.log` preserve the failing session. Private evidence/generated interop/backups remain ignored.

The new log loads both 0.8.1 plugins and has no previous OnDespawn trampoline flood or `Native skill visual source missing` failures. It records `PlayerData::AddInjury: P0: 11011 1` (Lead Belly), then the next skill ownership snapshot includes `11006 1` (Pick Your Moment). A matched checkpoint commits before the next arena. Those observations confirm acquisition/order; they do not prove Pick Your Moment caused the crash. The log ends during arena loading and contains no native fault stack. No matching game fault event or dump was found in the inspected Application events/local crash directories. Existing mesh/NetworkVariable/NavMesh warnings and DOTween warnings following F1 potion rerolls are recorded separately, not declared to be the cause.

Offline inspection of the unchanged GameAssembly and generated runtime wrappers found that `WeaponCharge.Attack(Vector3)` (native token 100669058) and `WeaponAxeCharge.Attack(Vector3)` (100669012) **share RVA `0x7c44d0`**. The old override loop patched both. Their shared body only conditionally delegates to the separately observed `Weapon.Attack`; observing both wrappers is unnecessary and installs overlapping native detours. This is an evidenced hook hazard, not a proven sole explanation for the access violation. An offline audit examined 241 candidate native endpoints and their shared-address peers; it is not a universal native safety proof.

The potion reveal coroutine calls `StringManager.SetInjuryNameTermAndNumerals(Localize, InjuryType, int)`. Its native body formats `Injuries/{enum}_Name` directly, bypassing our `GetInjuryNameTerm` hook. The new injuries lacked that native term, allowing the pooled label to retain its previous content. The description used a separate correctly hooked lookup, explaining the mixed name/description in the screenshot.

## Changes

- Both production plugins identify as **0.8.2**. GUIDs, IDs, schema 1, net6.0 and BepInEx 6.0.0-be.791+b782da4 stay the same. Game binaries/assets and saves are not manually edited.
- Leave the two shared charged-weapon Attack wrappers untouched. Their real base `Weapon.Attack` call remains observed, alongside the existing unique effect/payment paths. `NativeHookPolicy` also refuses those wrappers explicitly.
- `FeaturePatcher` resolves the native method entry using the installed Il2CppInterop helper and inspected MethodInfo layout. `NativeEntryClaims` rejects different wrappers sharing an entry before installing a second detour; multiple features patching the same wrapper remain allowed. Claims commit only after a successful patch. The guard covers users of this helper, not foreign plugins patching directly. Il2CppInterop.Common is an existing loader dependency, referenced with `Private=false`; no new dependency is deployed.
- A custom-only prefix on `StringManager.SetInjuryNameTermAndNumerals` assigns registered `RevealName/0..3` terms for the ten new injuries, including **Lead Belly I/II/III**. Native injuries continue through the original helper; legacy Leadfoot already has its own native aliases. No reveal-coroutine detour, global string replacement or tooltip scan is added.
- Shortened all new skill/equipment/potion/surgery effect text. Every new per-rank/severity description is at most 125 characters. Trainer offers describe the rank being bought, while character/tooltips/reveals describe the owned rank. This deliberately replaces the long combined current/next paragraphs at the user's request; mechanics/formulas are unchanged. Existing short Wraps/Leadfoot text remains, Bulk Up legacy summaries are shortened, and the manifest keeps prior detailed `mechanicsReference` text. Full mechanics remain in the original specification and category guides.

## Verification and deployment

Foundation, TermsAndContusions and both standalone/category example projects built Release/net6.0 with **zero warnings/errors**. **1,729 nongame checks pass**: Stages 1–6 retain 50/111/521/191/271/321 checks; Stage 7 has 230 including native-entry conflicts, known unsafe wrappers, bounded text, name/severity formatting and installed helper metadata; 34 synthetic deployment/rollback/archive checks pass. The explicit deployment-refusal guard also passes. No native game object or live handler was initialized by these checks.

Both **0.8.2 DLLs were deployed through the existing explicit workflow with the game closed**. Deployment record: `artifacts/packages/0.8.2/deployment.json`. Immediate pre-deploy matched checkpoint: `backups/expansion-stage7-deploy-20261026-081208-528`. It contains the failing 0.8.1 DLLs plus current configuration and full user data; the earlier crash2 checkpoint retains the pre-fix source/logs too. Rollback preserves recoverability, not a claim that 0.8.1 is safe to play.

| Installed DLL | SHA-256 |
|---|---|
| ChainedBeasts.ModFoundation 0.8.2 | `328da9d33e411210134dd872e755f50469d8c4a6486d6c4c5efca5d480bc7260` |
| TermsAndContusions 0.8.2 | `ccc4c982bfb46db5ca84106bb5306122612429aa859c6e11803764e4eb2dcdbd` |

All **3 configuration and 16 current user-data files** match the pre-fix crash2 checkpoint byte-for-byte, with no new/removed files in those trees. Native GameAssembly SHA-256 remains `72f2892607b0772470caae5db766dc07aaf636c3edfe26cefe019a5b9ddae56b`; the original expansion specification hash is unchanged. Installed/build/ZIP DLL hashes match. Local packages remain ignored under `artifacts/packages/0.8.2/`; combined ZIP SHA-256 `79eb0b7264ebe98ee06a947bad6cafdda80ecd7d00ae009d75add7eff789885e`. Current local documentation links resolve. These are nongame verification results; they cannot establish native runtime success or visual fit.

To deliberately restore the immediate matched set with the game closed:

```powershell
pwsh -NoProfile -File scripts/restore-plugin-backup.ps1 -Restore -BackupRoot backups/expansion-stage7-deploy-20261026-081208-528
```

Preserve any newer run first: paired restoration replaces current user data/configuration too. Do not downgrade by swapping only one DLL or restore an old native save without its matching sidecars. Source restoration is separate, using the crash2 checkpoint's source directories. No actual rollback was performed.

**Manual Solo retest pending:** confirm both 0.8.2 startup lines and no patch-entry conflict error; inspect a Lead Belly potion/reveal and short cards in each category; acquire Pick Your Moment through normal selection, then play the next arena using normal melee/charged/thrown weapons. Check that skill offers show the next rank and owned views show the actual rank. Do not treat historical unverified catalog rows as passed. If the hard crash returns, preserve the fresh logs before another launch; a native dump may be needed to identify the remaining cause.

See [installation and paired rollback](INSTALLATION.md), [native hook safety](foundation/NATIVE_HOOK_SAFETY.md), [the earlier crash](CRASH_2026-10-26.md) and [manual checks](MANUAL_TESTS.md). Nothing was pushed or published.
