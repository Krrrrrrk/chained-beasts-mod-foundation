# Friend installer

`INSTALL.bat` runs the included PowerShell helper on Windows PowerShell 5.1.
The distributable contains the **official BepInEx IL2CPP x64 build 791 archive**,
**Foundation + Terms & Contusions 0.8.3**, and **Battle Numbers 0.1.1**.
It is a separate convenience bundle; the normal GitHub plugin ZIPs still exclude
the loader. No game files, interop bindings, personal configs, saves or logs are
included. Nothing is launched or published by this workflow.

Build the ZIP with Python 3 (internet required while preparing uncached downloads):

```powershell
python scripts/package-friend-installer.py
```

The builder pins and verifies SHA-256 for the loader, plugins and Unity reference
ZIP. When the supplied extracted loader directory exists under artifacts, all
228 files must match the official archive exactly; no local extras are shared.
Downloads are
cached under ignored `downloads/friend-installer`. Output and its checksum are in
ignored `artifacts/friend-installer`. If the output staging folder already exists,
preserve/rename it before building again; the builder refuses to merge old files.
Ship the complete generated ZIP, including licenses, source archives and helpers.
Your friend does not need Python, PowerShell 7 or build tools.

The installer displays its search progress, checks Steam's registry and VDF
libraries/manifests, common paths on fixed local drives, then a bounded fallback
search (35 seconds, depth 8, up to 20,000 folders; no junctions/network drives).
It automatically uses one compatible result. A folder picker is only needed if
none or multiple compatible registered copies are found. It checks the
inspected GameAssembly hash and that the game is closed. Conflicting loader files
or a different loader cause a refusal before installation. An identical existing
loader is reused. All bundled files are checked again before installation.

Backups go to `%LOCALAPPDATA%\ChainedBeastsModInstaller\Backups`, outside the game.
Each records the complete prior plugin/config/user-data file contents, hashes,
installer additions, overwritten originals and removed duplicate plugin copies.
Renamed/nested copies are detected by assembly metadata without loading them.
Unrelated mods and configuration are preserved except for BepInEx's
`[IL2CPP] UnityBaseLibrariesSource`, which is set to the bundled local filename.
The original config is backed up; unrelated sections/values are kept. A redirected
interop cache or ambiguous IL2CPP config refuses before modifying the game.
A copy failure rolls
back the files already attempted; if rollback cannot finish, the console gives the
backup location for manual recovery. Each backup includes `ROLLBACK.txt`.

Fresh installs inherit the published defaults: normal offers, no forced content,
cheats initially off. Existing preferences are not reset. Installation itself is
offline, including BepInEx dependency setup: `BepInEx/unity-libs/6000.3.21.zip`
is pinned to the Phase 2 SHA-256 and `UnityBaseLibrariesSource = 6000.3.21.zip`
has no HTTP fallback. Build 791's inspected Il2CppInteropManager reads and extracts
that local ZIP. The first game launch generates bindings locally; generated game
bindings are never shared. The game's own Steam/network behavior is unchanged.

The BAT uses execution-policy bypass **only for its child process**. It does not
alter permanent security settings. If the game directory denies write access,
the installer requests interactive UAC elevation and waits for the result. The
child checks the caller's Windows SID before writing, so elevation under another
account cannot silently back up the wrong saves. Actual UAC acceptance still
requires the user's Windows prompt and is not automated by filesystem checks.
`UNINSTALL.bat` asks for explicit confirmation and undoes the bundle using the
earliest active installation record for each path, so no-op reinstalls do not
lose the original baseline. It restores overwritten/removed previous plugin files
and removes files the bundle added. It makes a fresh current plugin/config/save
backup before changes. Hash conflicts or missing original backups refuse before
removal; copy/delete failures restore attempted files and installation statuses.

A loader added by the bundle is removed only if all loader files were originally
absent, its installed files still match, and no remaining/restored plugin or
patcher needs it. Otherwise the entire loader is retained. Generated interop,
logs, caches, configs and all saves are always preserved, including the offline
reference ZIP and its config. Earlier installer records without those optional
files are accepted. The uninstaller never
restores saves. Finish/abandon a run owning custom content before removing it;
continuing a pre-mod run instead needs its full matching save/config backup.
The same uninstaller can read records created by the earlier installer ZIP. It
does not support arbitrary manual installs or multiple independent Windows-user
backup stores. Do not discard the original installer backups.

The installer is checked with temporary filesystem fixtures under Windows
PowerShell 5.1. The combined bundle still needs your friend's manual first launch.
