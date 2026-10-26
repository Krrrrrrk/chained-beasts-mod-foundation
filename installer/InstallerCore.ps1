# All paths are literal; no downloaded code is run and no game binaries are loaded.
Set-StrictMode -Version Latest
function Get-Sha([string]$Path) {
    # Stream through the framework API; no optional PowerShell modules required.
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}
function Child-Path([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or
        $Relative.Contains(':') -or $Relative -match '(^|[\\/])\.\.?([\\/]|$)') { throw "Unsafe relative path: $Relative" }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\','/') + '\'
    $path = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (!$path.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped root: $Relative" }
    # Do not follow junctions or symbolic links into unrelated locations.
    $cursor = $path
    while ($cursor.Length -ge $base.TrimEnd('\').Length) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -Force -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked path refused: $cursor" }
        }
        $parent = Split-Path -Parent $cursor
        if (!$parent -or $parent -eq $cursor) { break }
        $cursor = $parent
    }
    $path
}
function Relative-Path([string]$Root, [string]$Path) {
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\','/') + '\'
    if (!$Path.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw 'File is outside expected root.' }
    $Path.Substring($base.Length).Replace('\','/')
}
function Copy-Checked([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    if ((Get-Sha $Source) -ne (Get-Sha $Destination)) { throw "Copy verification failed: $Destination" }
}
function Assert-GameClosed {
    if (@(Get-Process -Name ChainedBeasts -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Close Chained Beasts, then run the installer or uninstaller again. Nothing was terminated.' }
}
function Test-GameFolderWritable([string]$GameRoot) {
    $probe = Child-Path $GameRoot ('.mod-install-access-'+[Guid]::NewGuid().ToString('N'))
    try { [IO.File]::WriteAllText($probe,'permission check'); $true }
    catch {
        if ($_.Exception.GetBaseException() -is [UnauthorizedAccessException]) { $false }
        else { throw }
    } finally { if (Test-Path -LiteralPath $probe -PathType Leaf) { Remove-Item -LiteralPath $probe } }
}
function Start-ElevatedInstall([string]$ScriptPath, [string]$GameRoot) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Windows still denies access to the game folder while elevated. Check folder permissions.' }
    foreach ($value in @($ScriptPath,$GameRoot)) { if ($value -match '["\r\n]') { throw 'Unsupported path characters in elevation request.' } }
    $arguments = '-NoProfile -ExecutionPolicy Bypass -STA -File "'+$ScriptPath+'" -GameRoot "'+$GameRoot+'" -ExpectedUserSid '+$identity.User.Value
    Write-Host 'Windows needs permission to write to this Steam folder. Please accept the UAC prompt using your own account.'
    # This interactive installer window must remain visible to the user.
    $process = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe') -Verb RunAs -ArgumentList $arguments -PassThru -Wait
    if ($null -eq $process.ExitCode) { throw 'The elevated installer did not report a result. Check its window and backup records before retrying.' }
    $process.ExitCode
}
function Assert-SupportedGame([string]$GameRoot, $Bundle) {
    foreach ($name in @('ChainedBeasts.exe','GameAssembly.dll','UnityPlayer.dll','ChainedBeasts_Data/il2cpp_data/Metadata/global-metadata.dat')) {
        if (!(Test-Path -LiteralPath (Child-Path $GameRoot $name) -PathType Leaf)) { throw "Not a complete Chained Beasts installation: missing $name" }
    }
    if ((Get-Sha (Child-Path $GameRoot 'GameAssembly.dll')) -ne $Bundle.gameAssemblySha256) {
        throw 'This game build differs from the tested Steam 1.0.1 build. Nothing installed. This bundle needs updating for that build.'
    }
}
function Find-SteamGame {
    $steamRoots = @("${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam")
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $reg = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($reg) {
            foreach ($name in @('SteamPath','InstallPath')) {
                if ($reg.PSObject.Properties[$name]) { $steamRoots += [string]$reg.$name }
            }
        }
    }
    $libraries = @($steamRoots)
    foreach ($root in @($steamRoots | Select-Object -Unique)) {
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf -PathType Leaf) {
            $text = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($text, '"(?:path|[0-9]+)"\s+"([^"]+)"')) {
                $candidate = $match.Groups[1].Value.Replace('\\','\')
                if ([IO.Path]::IsPathRooted($candidate)) { $libraries += $candidate }
            }
        }
    }
    foreach ($root in @($libraries | Select-Object -Unique)) {
        $game = Join-Path $root 'steamapps\common\Chained Beasts'
        if (Test-Path -LiteralPath (Join-Path $game 'ChainedBeasts.exe') -PathType Leaf) { $game }
    }
}
function Read-Bundle([string]$BundleRoot) {
    $bundle = Get-Content -LiteralPath (Child-Path $BundleRoot 'bundle.json') -Raw | ConvertFrom-Json
    if ($bundle.schemaVersion -ne 1 -or $bundle.files.Count -lt 4) { throw 'Missing or unsupported bundle manifest. Extract the complete ZIP again.' }
    foreach ($file in $bundle.payloads) {
        $source = Child-Path $BundleRoot $file.path
        if ((Get-Sha $source) -ne $file.sha256) { throw "Damaged bundle file: $($file.path). Extract the complete ZIP again." }
    }
    $seen = @{}
    foreach ($file in $bundle.files) {
        $null = Child-Path $BundleRoot $file.path
        if ($seen.ContainsKey($file.path)) { throw 'Duplicate destination in bundle.' }
        $seen[$file.path] = $true
        if ($file.path -notmatch '^(BepInEx[\\/](core[\\/]|plugins[\\/])|dotnet[\\/])' -and
            $file.path -notin @('.doorstop_version','doorstop_config.ini','changelog.txt','winhttp.dll','BepInEx/unity-libs/6000.3.21.zip','BepInEx/config/BepInEx.cfg')) { throw "Unexpected installation target: $($file.path)" }
    }
    $bundle
}
function Set-OfflineUnitySource([string]$StagedConfig, [string]$InstalledConfig) {
    if (!(Test-Path -LiteralPath $InstalledConfig -PathType Leaf)) { return }
    $text = [IO.File]::ReadAllText($InstalledConfig)
    $sections = [regex]::Matches($text,'(?im)^[ \t]*\[IL2CPP\][ \t]*\r?$')
    if ($sections.Count -gt 1) { throw 'Multiple [IL2CPP] sections in BepInEx.cfg; offline setup refused.' }
    if ($sections.Count -eq 0) { $text = $text.TrimEnd()+"`r`n`r`n[IL2CPP]`r`nUnityBaseLibrariesSource = 6000.3.21.zip`r`n" }
    else {
        $start = $sections[0].Index+$sections[0].Length
        $next = [regex]::Match($text.Substring($start),'(?m)^[ \t]*\[')
        $length = $text.Length-$start
        if ($next.Success) { $length = $next.Index }
        $section = $text.Substring($start,$length)
        $path = [regex]::Match($section,'(?im)^[ \t]*IL2CPPInteropAssembliesPath[ \t]*=[ \t]*([^\r\n]*)')
        if ($path.Success -and $path.Groups[1].Value.Trim() -ne '{BepInEx}') { throw 'Custom IL2CPPInteropAssembliesPath in BepInEx.cfg needs manual offline setup. Nothing installed.' }
        $keys = [regex]::Matches($section,'(?im)^[ \t]*UnityBaseLibrariesSource[ \t]*=[^\r\n]*')
        if ($keys.Count -gt 1) { throw 'Duplicate UnityBaseLibrariesSource settings; offline setup refused.' }
        if ($keys.Count -eq 1) {
            $section = [regex]::Replace($section,'(?im)^[ \t]*UnityBaseLibrariesSource[ \t]*=[^\r\n]*','UnityBaseLibrariesSource = 6000.3.21.zip')
        } else { $section = "`r`nUnityBaseLibrariesSource = 6000.3.21.zip`r`n"+$section }
        $text = $text.Substring(0,$start)+$section+$text.Substring($start+$length)
    }
    [IO.File]::WriteAllText($StagedConfig,$text,(New-Object Text.UTF8Encoding($false)))
}
function Backup-Tree([string]$Source, [string]$Destination) {
    $record = @()
    if (Test-Path -LiteralPath $Source -PathType Container) {
        # Validate directories before walking their files, including empty junctions.
        foreach ($item in @(Get-Item -Force -LiteralPath $Source) + @(Get-ChildItem -Force -LiteralPath $Source -Recurse)) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked backup path refused: $($item.FullName)" }
        }
        foreach ($file in @(Get-ChildItem -LiteralPath $Source -Force -Recurse -File)) {
            $relative = Relative-Path $Source $file.FullName
            Copy-Checked $file.FullName (Child-Path $Destination $relative)
            $record += [pscustomobject]@{path=$relative;sha256=(Get-Sha $file.FullName)}
        }
    }
    $record
}
function Invoke-BundleInstall([string]$BundleRoot, [string]$GameRoot, [string]$DataRoot, [string]$BackupBase) {
    Assert-GameClosed
    $bundle = Read-Bundle $BundleRoot
    Assert-SupportedGame $GameRoot $bundle
    $GameRoot = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
    foreach ($otherLoader in @('BepInEx/core/BepInEx.dll','BepInEx/core/BepInEx.Unity.Mono.dll','MelonLoader')) {
        if (Test-Path -LiteralPath (Child-Path $GameRoot $otherLoader)) { throw "Different mod loader detected: $otherLoader. Nothing installed; ask for help before combining loaders." }
    }
    foreach ($outside in @($BundleRoot,$BackupBase,$DataRoot)) {
        $full = [IO.Path]::GetFullPath($outside).TrimEnd('\','/')
        if ($full -eq $GameRoot -or $full.StartsWith($GameRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or
            $GameRoot.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Extract the bundle and keep backups outside the game folder.' }
    }
    $stage = Join-Path ([IO.Path]::GetTempPath()) ('ChainedBeastsModInstaller-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $backup = $null
    $attempted = @()
    $removed = @()
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead((Child-Path $BundleRoot 'payload/BepInEx.zip'))
        try {
            foreach ($entry in $archive.Entries) {
                $target = Child-Path $stage $entry.FullName
                if (!$entry.Name) { New-Item -ItemType Directory -Force -Path $target | Out-Null; continue }
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)
            }
        } finally { $archive.Dispose() }
        $stagedHashes = @{}
        foreach ($file in $bundle.files) {
            $target = Child-Path $stage $file.path
            if ($file.kind -in @('plugin','offline-cache','config')) { Copy-Checked (Child-Path $BundleRoot $file.payload) $target }
            if ((Get-Sha $target) -ne $file.sha256) { throw "Staged file mismatch: $($file.path)" }
            $installed = Child-Path $GameRoot $file.path
            if ($file.kind -eq 'config') { Set-OfflineUnitySource $target $installed }
            $stagedHashes[$file.path] = Get-Sha $target
            if (Test-Path -LiteralPath $installed -PathType Container) { throw "A directory conflicts with required file: $installed" }
            if ($file.kind -eq 'loader' -and (Test-Path -LiteralPath $installed -PathType Leaf) -and (Get-Sha $installed) -ne $file.sha256) {
                throw "An existing loader file differs: $($file.path). Nothing changed. Keep that installation and ask for help rather than mixing loaders."
            }
        }
        # Find our assemblies by metadata identity, including renamed copies. Never execute/load them.
        $duplicates = @()
        $plugins = Child-Path $GameRoot 'BepInEx/plugins'
        if (Test-Path -LiteralPath $plugins) {
            foreach ($file in @(Get-ChildItem -LiteralPath $plugins -Recurse -Force -Filter '*.dll' -File)) {
                $relative = Relative-Path $GameRoot $file.FullName
                $null = Child-Path $GameRoot $relative
                try { $identity = [Reflection.AssemblyName]::GetAssemblyName($file.FullName).Name } catch { continue }
                if ($identity -in $bundle.pluginNames -and $relative -notin @($bundle.files.path)) { $duplicates += $relative }
            }
        }
        # Check write permission before doing expensive backups or changing any installed files.
        $probe = Child-Path $GameRoot ('.mod-install-probe-'+[Guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($probe,'permission check')
        Remove-Item -LiteralPath $probe
        $backup = Join-Path $BackupBase ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
        New-Item -ItemType Directory -Path $backup | Out-Null
        $data = @(Backup-Tree $DataRoot (Join-Path $backup 'user-data'))
        $configs = @(Backup-Tree (Child-Path $GameRoot 'BepInEx/config') (Join-Path $backup 'config'))
        $pluginBackup = @(Backup-Tree $plugins (Join-Path $backup 'plugins'))
        $plan = @()
        foreach ($file in $bundle.files) {
            $target = Child-Path $GameRoot $file.path
            $exists = Test-Path -LiteralPath $target -PathType Leaf
            $oldSha = $null
            if ($exists) { $oldSha = Get-Sha $target }
            $newSha = $stagedHashes[$file.path]
            $changed = !$exists -or $oldSha -ne $newSha
            if ($exists -and $changed) { Copy-Checked $target (Child-Path (Join-Path $backup 'original-files') $file.path) }
            $plan += [pscustomobject]@{path=$file.path;existed=$exists;changed=$changed;previousSha256=$oldSha;installedSha256=$newSha}
        }
        foreach ($relative in $duplicates) { Copy-Checked (Child-Path $GameRoot $relative) (Child-Path (Join-Path $backup 'original-files') $relative) }
        $record = [ordered]@{schemaVersion=1;status='prepared';gameRoot=$GameRoot;userDataRoot=$DataRoot;bundleVersion=$bundle.version;
            files=$plan;removedDuplicates=$duplicates;userData=$data;config=$configs;plugins=$pluginBackup;createdUtc=[DateTime]::UtcNow.ToString('o')}
        $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $backup 'installation.json') -Encoding UTF8
        @'
Keep this entire backup folder. It was made BEFORE installation.

UNINSTALL.bat in the complete extracted bundle can undo the install using these
records. It preserves current saves/configs and first makes a fresh backup.
Finish or abandon runs owning removed custom content before uninstalling.

installation.json lists files added/replaced and duplicate plugin copies moved out.
original-files contains exact previous versions of replaced/moved files.
plugins, config and user-data contain the previous complete file contents with hashes.

For rollback, close Chained Beasts and first copy your CURRENT plugins, config and
complete LocalLow/Featherweight Games/ChainedBeasts folder somewhere safe.
For each files entry with changed=true: if existed=false, remove that specific
installed file; if existed=true, restore that path from original-files to gameRoot.
Copy removedDuplicates paths from original-files back to gameRoot.
Leave unchanged files, unrelated mods and generated loader files alone.

This restores installer changes ONLY. Game startup may generate interop assemblies,
logs, caches and config files afterward; these are not listed as installer additions.
Do not blindly delete the whole BepInEx folder if other mods or an older loader exist.

Do not remove owned custom content and then resume the same run. Finish/abandon
the run while the mod is installed, or restore the complete matching pre-mod saves
and config together. Save restoration loses later progress. Do not restore only
SaveData without its matching ModFoundation/checkpoints sidecars.
The installer never edits or restores saves automatically.
'@ | Set-Content -LiteralPath (Join-Path $backup 'ROLLBACK.txt') -Encoding UTF8
        Assert-GameClosed
        foreach ($relative in $duplicates) {
            $removed += $relative
            Remove-Item -LiteralPath (Child-Path $GameRoot $relative)
        }
        foreach ($file in @($plan | Where-Object changed)) {
            Assert-GameClosed
            $attempted += $file
            Copy-Checked (Child-Path $stage $file.path) (Child-Path $GameRoot $file.path)
        }
        $record.status = 'installed'
        $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $backup 'installation.json') -Encoding UTF8
        [pscustomobject]@{BackupRoot=$backup;FilesChanged=@($plan | Where-Object changed).Count;DuplicatesRemoved=$duplicates.Count}
    } catch {
        $failure = $_
        if ($attempted.Count -gt 0 -or $removed.Count -gt 0) {
            try {
                Assert-GameClosed
                foreach ($file in $attempted) {
                    $target = Child-Path $GameRoot $file.path
                    if ($file.existed) { Copy-Checked (Child-Path (Join-Path $backup 'original-files') $file.path) $target }
                    elseif (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target }
                }
                foreach ($relative in $removed) { Copy-Checked (Child-Path (Join-Path $backup 'original-files') $relative) (Child-Path $GameRoot $relative) }
                $record.status = 'rolled-back'
                $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $backup 'installation.json') -Encoding UTF8
                Write-Warning "Installation failed; changed files were rolled back. Backup: $backup"
            } catch { Write-Warning "Rollback needs attention: $($_.Exception.Message). Keep backup: $backup" }
        }
        throw $failure
    } finally {
        # Only our unique temporary staging directory; verify before recursive deletion.
        $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
        $fullStage = [IO.Path]::GetFullPath($stage)
        if ($fullStage.StartsWith($tempBase,[StringComparison]::OrdinalIgnoreCase) -and
            (Split-Path -Leaf $fullStage) -match '^ChainedBeastsModInstaller-[a-f0-9]{32}$') {
            Remove-Item -LiteralPath $fullStage -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
