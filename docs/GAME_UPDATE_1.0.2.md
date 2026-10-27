# Chained Beasts 1.0.2 compatibility update

The [**Foundation + Terms & Contusions 0.9.3 prerelease**](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/tag/v0.9.3)
and optional [**Battle Numbers 0.2.2 prerelease**](https://github.com/Krrrrrrk/Chained-Beasts_Battle-Numbers/releases/tag/v0.2.2)
target Steam **26231777**, game **1.0.2**, Unity **6000.3.21f1**, and the existing
BepInEx **6.0.0-be.791+b782da4** loader. My quick Solo check of the latest fixes
looks good. Broader mechanics, presentation, checkpoint and host/client testing
remain pending.

The installed game changed its native binary and regenerated interop. The old
mods correctly refused the uninspected build. This update keeps those guards
and targets the newly inspected binary; it does not bypass them.

## What changed

- Foundation's exact `TakeDamage` hook now matches nine arguments. The added
  `ignoreDodgeIFrames` flag stays under normal game control; its default is false.
- Throw windup and delayed-launch coroutine bindings now use the updated
  generated types. Charge timing, dispatch and launch paths were rechecked.
- Battle Numbers uses the newly verified native damage/health-change entries.
  It still measures committed HP loss; damage authority stays with the game.
- The existing HUD font is still `Skranji-Regular SDF`. Selection uses its name,
  so moved asset IDs don't break it. The font is not bundled or modified.
- Native checkpoint/ownership, surgery and resource paths were compared against
  the prior build. The custom state schema and all 46 content IDs are unchanged.
- Both projects were rebuilt against the loader's fresh runtime interop, retaining
  net6.0, plugin GUIDs, configuration identities and the previous mechanics fixes.

Inspected `GameAssembly.dll` SHA256:
`ba3ef02fa87da4c41bb27d7610539c9578132204f0fb5713833e2ef93415ce29`.

## Install the matching build

Close the game and back up plugins, configuration and the complete user-data
folder, including Foundation checkpoint sidecars. Keep exactly one copy of each
plugin. Download the [combined gameplay ZIP](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.9.3/TermsAndContusions-with-Foundation-0.9.3.zip)
or the [all-three ZIP with Battle Numbers](https://github.com/Krrrrrrk/chained-beasts-mod-foundation/releases/download/v0.9.3/Chained-Beasts-Multiplayer-Test-0.9.3.zip).
For source builds, the package workflow produces the same choices locally:

```text
artifacts/packages/0.9.3/TermsAndContusions-with-Foundation-0.9.3.zip
artifacts/packages/0.9.3/Chained-Beasts-Multiplayer-Test-0.9.3.zip
```

The second ZIP includes Battle Numbers 0.2.2. It contains only mod DLLs, notices
and instructions; keep the existing loader and let BepInEx generate game bindings
on each PC. Don't copy another PC's interop assemblies. Existing configuration
is preserved, including normal offer pools and optional activation feedback.

Foundation/TC releases through 0.9.1 and Battle Numbers through 0.2.0 target game 1.0.1.
They haven't been replaced and should not be used for this updated game.
See [multiplayer setup](MULTIPLAYER.md) for matching-party requirements.

## Verification and rollback

The production projects, both teaching examples and nongame checks pass against
the new bindings. Those checks cover logic, serialization, identities, metadata,
native entry safety and reversible packaging/deployment. They don't establish
that Unity callbacks or a resumed run work in live gameplay.

For further manual testing, check startup logs for all three new versions and no
hook/interop errors. Check ordinary outgoing/incoming numbers, a custom skill
and its banner, throws, an execution-heal item, a surgery, and a normal checkpoint
and resume. Test Solo first, then a matching host/client party if desired. Keep
both `Player.log` and `BepInEx/LogOutput.log` if something fails.

Backups preserve the prior mod/config/save set. Restoring them does **not**
undo Steam's game update: the older guarded mods remain incompatible with
game 1.0.2. Restoring saves also discards progress made after that backup.
Preserve newer progress before a paired rollback; don't overwrite saves merely
to switch a cosmetic font. Existing multiplayer limitations remain documented
in the setup guide.
