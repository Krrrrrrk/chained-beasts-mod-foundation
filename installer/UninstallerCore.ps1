# Dot-source InstallerCore.ps1 first. All removal targets are validated exact files.
Set-StrictMode -Version Latest
function Get-BundleInstallRecords([string]$BackupBase, [string]$Version) {
    if (!(Test-Path -LiteralPath $BackupBase -PathType Container)) { return }
    foreach ($folder in @(Get-ChildItem -LiteralPath $BackupBase -Directory)) {
        $path = Child-Path $BackupBase ($folder.Name+'/installation.json')
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        try { $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
        catch { throw "Unreadable installation record: $path. Nothing removed; keep the backup and ask for help." }
        if ($record.schemaVersion -eq 1 -and $record.status -eq 'installed' -and $record.bundleVersion -eq $Version) {
            [pscustomobject]@{Root=$folder.FullName;Path=$path;Record=$record}
        }
    }
}
function Get-BundleUninstallPlan([string]$BundleRoot, [string]$GameRoot, [string]$BackupBase) {
    Assert-GameClosed
    $bundle = Read-Bundle $BundleRoot
    $game = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
    if (!(Test-Path -LiteralPath (Child-Path $game 'ChainedBeasts.exe') -PathType Leaf)) { throw 'Selected game folder is missing ChainedBeasts.exe. Nothing removed.' }
    foreach ($outside in @($BackupBase,$BundleRoot)) {
        $full = [IO.Path]::GetFullPath($outside).TrimEnd('\','/')
        if ($full -eq $game -or $full.StartsWith($game+'\',[StringComparison]::OrdinalIgnoreCase) -or
            $game.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Bundle and backups must stay outside the game folder.' }
    }
    $records = @(Get-BundleInstallRecords $BackupBase $bundle.version | Where-Object {
        [IO.Path]::GetFullPath($_.Record.gameRoot).TrimEnd('\','/') -eq $game
    } | Sort-Object { [DateTime]$_.Record.createdUtc }, Path)
    if ($records.Count -eq 0) { throw 'No active installation record matches this game folder. Nothing removed.' }
    $allowed = @{}
    foreach ($file in $bundle.files) { $allowed[$file.path] = $file }
    $first = @{}
    $duplicateOriginals = @{}
    foreach ($snapshot in $records) {
        $seen = @{}
        $required = @($bundle.files | Where-Object { !$_.PSObject.Properties['optional'] -or !$_.optional })
        if ($snapshot.Record.files.Count -lt $required.Count -or $snapshot.Record.files.Count -gt $bundle.files.Count) { throw 'Incomplete installation record; nothing removed.' }
        foreach ($file in $snapshot.Record.files) {
            $null = Child-Path $game $file.path
            if (!$allowed.ContainsKey($file.path) -or $seen.ContainsKey($file.path) -or
                ($allowed[$file.path].kind -ne 'config' -and $file.installedSha256 -ne $allowed[$file.path].sha256) -or
                $file.installedSha256 -notmatch '^[a-fA-F0-9]{64}$' -or
                $file.existed -isnot [bool] -or $file.changed -isnot [bool]) { throw 'Installation record does not match this bundle; nothing removed.' }
            if (($file.existed -and $file.previousSha256 -notmatch '^[a-fA-F0-9]{64}$') -or
                (!$file.existed -and !$file.changed) -or
                ($file.existed -and ($file.changed -ne ($file.previousSha256 -ne $file.installedSha256)))) { throw 'Inconsistent installation record; nothing removed.' }
            $seen[$file.path] = $true
            # Earliest active record is the baseline. Later no-op reinstalls must
            # not lose ownership of files that the first install introduced.
            if (!$first.ContainsKey($file.path)) { $first[$file.path] = [pscustomobject]@{File=$file;Snapshot=$snapshot} }
        }
        foreach ($file in $required) { if (!$seen.ContainsKey($file.path)) { throw 'Missing required file in installation record.' } }
        foreach ($relative in $snapshot.Record.removedDuplicates) {
            $null = Child-Path $game $relative
            if ($relative -notmatch '^BepInEx[\\/]plugins[\\/].+\.dll$' -or $allowed.ContainsKey($relative)) { throw 'Unsafe duplicate path in installation record.' }
            if ($duplicateOriginals.ContainsKey($relative)) { continue }
            $pluginRelative = $relative.Substring('BepInEx/plugins/'.Length).Replace('\','/')
            $recorded = @($snapshot.Record.plugins | Where-Object { $_.path.Replace('\','/') -eq $pluginRelative })
            if ($recorded.Count -ne 1 -or $recorded[0].sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Missing original duplicate hash; nothing removed.' }
            $source = Child-Path (Join-Path $snapshot.Root 'original-files') $relative
            if ((Get-Sha $source) -ne $recorded[0].sha256) { throw "Backup hash mismatch: $source" }
            $identity = [Reflection.AssemblyName]::GetAssemblyName($source).Name
            if ($identity -notin $bundle.pluginNames) { throw 'Backup duplicate does not belong to this bundle.' }
            $duplicateOriginals[$relative] = [pscustomobject]@{Path=$relative;Source=$source;Sha=$recorded[0].sha256}
        }
    }
    $operations = @()
    foreach ($entry in $first.Values) {
        $file = $entry.File
        # Keep the local reference cache and current loader settings together.
        # BepInEx rewrites config during startup; never erase newer preferences.
        if ($allowed[$file.path].kind -in @('config','offline-cache')) { continue }
        if (!$file.changed) { continue }
        $source = $null
        if ($file.existed) {
            $source = Child-Path (Join-Path $entry.Snapshot.Root 'original-files') $file.path
            if ((Get-Sha $source) -ne $file.previousSha256) { throw "Backup hash mismatch: $source" }
        }
        $operations += [pscustomobject]@{Path=$file.path;Kind=$allowed[$file.path].kind;Restore=$source;Expected=$file.installedSha256;Previous=$file.previousSha256;BeforeExists=$false;BeforeSha=$null}
    }
    foreach ($duplicate in $duplicateOriginals.Values) {
        $operations += [pscustomobject]@{Path=$duplicate.Path;Kind='duplicate';Restore=$duplicate.Source;Expected=$null;Previous=$duplicate.Sha;BeforeExists=$false;BeforeSha=$null}
    }
    # Remove a fresh loader only when nothing else will need it afterward.
    # Preserve the entire loader if any original loader file existed, a previous
    # plugin will be restored, unrelated plugins/patchers exist, or loader files
    # have changed. Generated config, interop and logs are always left alone.
    $keepLoader = @($first.Values | Where-Object { $allowed[$_.File.path].kind -eq 'loader' -and $_.File.existed }).Count -gt 0
    $reason = 'it was present before this bundle'
    if (@($operations | Where-Object { $_.Kind -in @('plugin','duplicate') -and $_.Restore }).Count -gt 0) {
        $keepLoader = $true; $reason = 'previous plugin files are being restored'
    }
    $pluginChanges = @{}
    foreach ($op in @($operations | Where-Object Kind -eq 'plugin')) { $pluginChanges[$op.Path] = $op }
    foreach ($relativeRoot in @('BepInEx/plugins','BepInEx/patchers')) {
        $root = Child-Path $game $relativeRoot
        if (!(Test-Path -LiteralPath $root)) { continue }
        foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -File -Force -Filter '*.dll')) {
            $relative = Relative-Path $game $file.FullName
            $null = Child-Path $game $relative
            if ($relativeRoot -eq 'BepInEx/plugins' -and !$first.ContainsKey($relative) -and !$duplicateOriginals.ContainsKey($relative)) {
                $identity = $null
                try { $identity = [Reflection.AssemblyName]::GetAssemblyName($file.FullName).Name } catch { }
                if ($identity -in $bundle.pluginNames) { throw "An untracked copy of a bundled plugin remains: $relative. Move it to a backup outside BepInEx/plugins, then rerun UNINSTALL.bat." }
            }
            if (!$pluginChanges.ContainsKey($relative) -or $pluginChanges[$relative].Restore) {
                $keepLoader = $true; $reason = 'other or pre-existing plugins/patchers need it'
            }
        }
    }
    foreach ($op in @($operations | Where-Object Kind -eq 'loader')) {
        $target = Child-Path $game $op.Path
        if ((Test-Path -LiteralPath $target) -and
            (!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-Sha $target) -ne $op.Expected)) {
            $keepLoader = $true; $reason = 'loader files have changed since installation'
        }
    }
    if ($keepLoader) { $operations = @($operations | Where-Object Kind -ne 'loader') }
    $ready = @()
    foreach ($op in $operations) {
        $target = Child-Path $game $op.Path
        if (Test-Path -LiteralPath $target) {
            if (!(Test-Path -LiteralPath $target -PathType Leaf)) { throw "A folder conflicts with uninstall target: $target" }
            $op.BeforeExists = $true
            $op.BeforeSha = Get-Sha $target
            if ($op.Restore -and $op.BeforeSha -eq $op.Previous) { continue }
            if ($op.BeforeSha -ne $op.Expected) { throw "File changed since installation: $($op.Path). Nothing removed. Back up the current file and ask for help." }
        } elseif (!$op.Restore) { continue }
        $ready += $op
    }
    $message = 'The newly installed loader files will be removed; generated caches, logs and settings are kept.'
    if ($keepLoader) { $message = "BepInEx is kept because $reason." }
    [pscustomobject]@{GameRoot=$game;Records=$records;Operations=@($ready | Sort-Object Path);LoaderMessage=$message}
}
function Invoke-BundleUninstall([string]$BundleRoot, [string]$GameRoot, [string]$DataRoot, [string]$BackupBase) {
    $plan = Get-BundleUninstallPlan $BundleRoot $GameRoot $BackupBase
    $data = [IO.Path]::GetFullPath($DataRoot).TrimEnd('\','/')
    $base = [IO.Path]::GetFullPath($BackupBase).TrimEnd('\','/')
    foreach ($other in @($plan.GameRoot,$base)) {
        if ($data -eq $other -or $data.StartsWith($other+'\',[StringComparison]::OrdinalIgnoreCase) -or
            $other.StartsWith($data+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Save backup must not overlap the game or installer backups.' }
    }
    Assert-GameClosed
    $backup = Join-Path $base ('before-uninstall-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $backup | Out-Null
    $saves = @(Backup-Tree $DataRoot (Join-Path $backup 'user-data'))
    $configs = @(Backup-Tree (Child-Path $plan.GameRoot 'BepInEx/config') (Join-Path $backup 'config'))
    $plugins = @(Backup-Tree (Child-Path $plan.GameRoot 'BepInEx/plugins') (Join-Path $backup 'plugins'))
    foreach ($op in $plan.Operations) {
        $target = Child-Path $plan.GameRoot $op.Path
        if ($op.BeforeExists) { Copy-Checked $target (Child-Path (Join-Path $backup 'current-files') $op.Path) }
    }
    $recordBytes = @{}
    foreach ($snapshot in $plan.Records) { $recordBytes[$snapshot.Path] = [IO.File]::ReadAllBytes($snapshot.Path) }
    $audit = [ordered]@{schemaVersion=1;status='prepared';gameRoot=$plan.GameRoot;userDataRoot=$DataRoot;operations=$plan.Operations;
        originalRecords=@($plan.Records.Path);userData=$saves;config=$configs;plugins=$plugins;createdUtc=[DateTime]::UtcNow.ToString('o')}
    $auditPath = Join-Path $backup 'uninstall.json'
    $audit | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $auditPath -Encoding UTF8
    @'
This backup was made immediately before UNINSTALL.bat changed any game files.
uninstall.json lists the exact operations. current-files holds files as they
were before uninstall; plugins, config and user-data are current snapshots.
No saves or settings were restored or edited by the uninstaller.

For recovery from an interrupted uninstall, close the game and keep another copy
of your current files first. For each attempted operation, restore its Path from
current-files if BeforeExists=true. If BeforeExists=false, remove ONLY that exact
Path if it was restored by the uninstaller. Leave all other files alone.
Do not restore old saves without the matching full sidecar/config set. Restoring
any save backup loses progress since that backup. Keep all installer backups.
'@ | Set-Content -LiteralPath (Join-Path $backup 'RECOVERY.txt') -Encoding UTF8
    $attempted = @()
    try {
        foreach ($op in $plan.Operations) {
            Assert-GameClosed
            $target = Child-Path $plan.GameRoot $op.Path
            $exists = Test-Path -LiteralPath $target -PathType Leaf
            if ($exists -ne $op.BeforeExists -or ($exists -and (Get-Sha $target) -ne $op.BeforeSha)) { throw "File changed during uninstall: $($op.Path)" }
            if ($op.Restore -and (Get-Sha $op.Restore) -ne $op.Previous) { throw "Original backup changed during uninstall: $($op.Path)" }
            $attempted += $op
            if ($op.Restore) { Copy-Checked $op.Restore $target }
            else { Remove-Item -LiteralPath $target }
        }
        foreach ($snapshot in $plan.Records) {
            $snapshot.Record.status = 'uninstalled'
            $snapshot.Record | Add-Member -NotePropertyName uninstalledUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('o')) -Force
            $snapshot.Record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $snapshot.Path -Encoding UTF8
        }
        $audit.status = 'uninstalled'
        $audit | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $auditPath -Encoding UTF8
        [pscustomobject]@{BackupRoot=$backup;FilesChanged=$attempted.Count;LoaderMessage=($plan.LoaderMessage -replace 'will be removed','were removed')}
    } catch {
        $failure = $_
        try {
            Assert-GameClosed
            foreach ($op in $attempted) {
                $target = Child-Path $plan.GameRoot $op.Path
                if ($op.BeforeExists) { Copy-Checked (Child-Path (Join-Path $backup 'current-files') $op.Path) $target }
                elseif (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target }
            }
            foreach ($path in $recordBytes.Keys) { [IO.File]::WriteAllBytes($path,$recordBytes[$path]) }
            $audit.status = 'rolled-back'
            $audit | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $auditPath -Encoding UTF8
            Write-Warning "Uninstall failed; file changes were rolled back. Recovery backup: $backup"
        } catch { Write-Warning "Uninstall recovery needs attention: $($_.Exception.Message). Keep backup: $backup" }
        throw $failure
    }
}
