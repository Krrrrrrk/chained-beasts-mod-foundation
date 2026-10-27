# Multiplayer setup

Use **Foundation + Terms & Contusions 0.9.6** and optional **Battle Numbers 0.2.2**
for **Chained Beasts 1.0.2 / Steam build 26231777**.

- Multiplayer testing has gone well so far, but I still expect weird edge cases.

The detailed checks below still need broader coverage. I have made no attempt
to balance the additions.

Download the [0.9.6 three-mod ZIP](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.9.6/Chained-Beasts-Multiplayer-Test-0.9.6.zip), or the [Foundation + TC ZIP](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.9.6/TermsAndContusions-with-Foundation-0.9.6.zip) and [Battle Numbers 0.2.2](https://github.com/Krrrrrrk/Chained-Beasts_Battle-Numbers/releases/tag/v0.2.2) separately. BepInEx remains a [separate installation](INSTALLATION.md#2-install-bepinex-once).

## Client transition fixes

Client ownership sync and native restoration now retain zero-rank custom skill
and injury entries. The trainer expects every registered skill to be present,
even when unowned. Deleting those entries caused the confirmed Breathing Room
`11010` lookup failure and interrupted the client's round-end lobby setup.

The release also keeps checkpoint transfer matching separate from strict saved-
file fingerprints, preserves agreements across ordinary feature resets, and
skips scene-dependent work until live player objects are available. These changes
use state assignment; they do not grant acquisitions, heal players or edit saves.
All 46 content IDs and the saved-file schema are unchanged.

The latest changes pass builds and nongame regressions, including native-style
trainer lookups after empty, mixed and repeated ownership sync. Multiplayer play
has also been working in my manual tests. For further coverage, start with a
fresh disposable run, finish two fights, and check normal offers and character
control on both PCs after each.
Test save/resume and Solo restore separately. If a pending-health warning remains,
keep both logs; confirmations are not discarded to force a checkpoint through.

Earlier Foundation/TC releases through 0.9.1 and Battle Numbers through 0.2.0
target game 1.0.1 and do not support the updated game. See
[game-update notes](GAME_UPDATE_1.0.2.md).

## Install on every PC

1. Close Chained Beasts on each PC. Back up the whole `BepInEx/plugins`,
   `BepInEx/config` and `%USERPROFILE%/AppData/LocalLow/Featherweight Games/ChainedBeasts`
   folders together, including `ModFoundation/checkpoints`.
2. Keep BepInEx **6.0.0-be.791+b782da4 Unity IL2CPP x64**, game 1.0.2 / Steam
   build 26231777 / Unity 6000.3.21f1. Generate interop locally through the loader;
   don't copy someone else's generated game bindings.
3. Remove old copies of these three plugin assemblies from every plugin
   subfolder, including `BattleNumbers`. Extract the multiplayer ZIP's `BepInEx`
   folder into the game root. The 0.9.6 ZIP contains Foundation 0.9.6,
   TermsAndContusions 0.9.6 and BattleNumbers 0.2.2. It doesn't contain the loader.
4. Send the **same ZIP** to everyone. Match the content enable switches and
   surgery prices in `BepInEx/config/local.chainedbeasts.termsandcontusions.cfg`.
   Cosmetic Battle Numbers colors/scale/lifetime can differ. Forced offers and
   F1 cheats remain Solo-only, even if enabled in configuration.
5. Have everyone join the host's lobby **before starting or resuming the run**.
   The host waits for the compatibility handshake and acknowledgements. Look for
   `[Multiplayer] party compatibility confirmed; host and clients ready` in the
   host's `BepInEx/LogOutput.log`. It checks compiled gameplay DLL hashes,
   registered content/IDs/ranks/enables and prices. Mismatched or missing
   Foundation installs are disconnected; a missing handshake times out after
   20 seconds. This isn't intended for vanilla clients.

Start with a **fresh disposable online run**, with everyone present. Existing
Solo checkpoints are separate; this doesn't convert a Solo save into an online
run. Mid-run joining, reconnecting and host migration are not supported. If a
player disconnects, stop testing and resume together from a matching checkpoint
with the same host, roster and player-index ordering. Don't treat disconnect recovery as tested.

## What synchronizes

Native NGO RPCs still distribute acquisition and combat actions. The server
commits HP. Custom direct-hit charges, HP-loss rewards and executions use
server-confirmed HP receipts, so a client's stale HP replica cannot spend a
bonus or award a reward twice. Player input, stamina and generated follow-up
attacks run on that player's owner. The owner serializes the modified launch
force and angle for a normal throw; remote replicas use those same values without
applying another range factor. Bulk Up and surgery health changes update data replicas and the
server's real HP variable through the existing transition rules.

The host validates custom surgery requests, costs and ownership before sending
the normal surgery RPC. Shared custom gold effects are host-controlled;
Pickpocket and Loose Change require a matching confirmed hit and owner.
Mercenary's Ledger charges on the host. Absolute shared gold and paid Ledger
flags are periodically synchronized; HUD changes can lag by up to a second.

Each owner supplies bounded feature-state snapshots to the host. Before a day
transition, the host waits for settled owner state, then creates the native
checkpoint plus matching custom sidecar. Before resuming, clients cache and
acknowledge the exact custom envelope before the native restore runs. A failed
checkpoint agreement cancels the operation, logs the failure and disconnects
the failed party rather than writing incomplete custom state or silently waiting. The host remains the source of
the resumed checkpoint; keep the same host, roster and player-index ordering.

Battle Numbers remains cosmetic and standalone. Version 0.2.2 on the host
measures actual HP loss and sends it to subscribed player owners. Each client
shows its own outgoing/incoming numbers, rather than another player's hits.
Clients need a matching updated host to receive these receipts. It changes no damage or HP.

Battle Numbers **0.2.2** retains the Skranji HUD font update and 0.2.x cosmetic
protocol. Font choice is local and does not need to match other players. Its
appearance still needs broader manual checks.

For the mechanics review, latest direct-hit fixes, confirmed healing and optional local dust/swish cues,
see [activation feedback](ACTIVATION_FEEDBACK.md). Its known latency/weapon-transfer
limits are still awaiting manual tests.

## Manual two-PC checks — further verification needed

- With matching installs, join both players before starting a fresh online run.
  Confirm both logs have no hook, handshake or interop errors. Also check a
  mismatched content setting is refused in a separate disposable lobby.
- Acquire Bulk Up on the **client**, then on the host. Check the correct player's
  banner, current/max HP, health percentage and HUD on both PCs. Acquiring a
  rank must grant once. Also test removal/reacquisition through normal mechanics.
- Try a custom equipment item, potion affliction and surgery on each player.
  Check ownership, native cost, severity and actual movement/stamina/health.
  Buy a paid surgery and Adrenal Graft; verify shared gold and once-only history.
- In combat, check both directions of Battle Numbers, ordinary/charged/thrown
  attacks, Payback, Pick Your Moment, a reward-on-execution effect, Locket,
  Pickpocket, Loose Change and Ledger. Check blocked/zero-damage hits don't
  consume or reward twice. Try more than one client after the two-PC case.
- Complete a day transition, exit normally, rejoin the same host/roster, then
  resume. Check both players' ranks, afflictions, surgery history, HP and spent
  per-battle allowances without replayed health/gold rewards. Repeat in Solo
  to check the existing behavior. F1 mutations must refuse in multiplayer.

Include **host and client** `Player.log` and `BepInEx/LogOutput.log`, exact mod
versions, roster and the action that failed. Copy logs before relaunching.

## Limits and rollback

This is cooperative trusted-party synchronization, not anti-cheat. Third-party
Foundation gameplay providers must register their compatibility version, binary
and mechanics settings using `MultiplayerServices`; arbitrary other mods aren't
automatically negotiated. Full arena-instance/current-stamina reconstruction,
Steam Cloud sidecar sync, original icons and reliable surgery rerolls remain
outside this change. Boss hit coverage in Battle Numbers is still incomplete.
Host/client action ordering, latency, native enums/RPCs, thrown weapon durability
and save/restore still need the manual tests above. Generic skill tooltips without
a player argument fall back to the first local player; multiple local players
on one PC need extra UI verification.

Close the game before rolling back. Restore the **paired pre-test plugins,
configuration and complete user-data backup together** on each PC. Don't just
swap DLLs and continue a run owning incompatible custom content. Don't mix
0.9.6 gameplay DLLs with older releases. The local deploy script records its
backup in `artifacts/packages/0.9.6/deployment.json`; use
`scripts/restore-plugin-backup.ps1` with that checkpoint to restore the matching
set.

## Build and local handoff

The companion checkout belongs at `chained-beasts-battle-numbers` and has its own
Git history; it is ignored by this repository. Use its matching 0.2.2 source
when building the all-three package.

Independent content providers must register compatibility during plugin load:

```csharp
MultiplayerServices.RegisterCompatibility(PluginGuid, PluginVersion, "my-mechanics-settings");
MultiplayerServices.RegisterBinary(typeof(MyPlugin).Assembly);
```

Include all startup settings that change mechanics in the string. The registered
content catalog already contributes its IDs, enabled flags and maximum ranks.
This handshake does not by itself make a new mechanic safe for multiplayer;
its state mutations still need the correct native authority and synchronization.

```powershell
pwsh -NoProfile -File scripts/build-plugins.ps1
dotnet build chained-beasts-battle-numbers/src/ChainedBeasts.BattleNumbers/ChainedBeasts.BattleNumbers.csproj -c Release
pwsh -NoProfile -File scripts/package-plugins.ps1 -SkipBuild -IncludeBattleNumbers
# Only after the game is closed; backs up plugins/configs/user data together:
pwsh -NoProfile -File scripts/build-and-deploy-plugin.ps1 -Deploy -SkipBuild -IncludeBattleNumbers
```

The combined handoff is
`artifacts/packages/0.9.6/Chained-Beasts-Multiplayer-Test-0.9.6.zip`. No build,
check or package command launches the game or publishes a GitHub release.
