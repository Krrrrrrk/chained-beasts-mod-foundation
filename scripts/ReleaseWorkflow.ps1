# Shared local-only packaging/deployment helpers. Dot-source; importing never builds, deploys or launches.
Set-StrictMode -Version Latest
$script:ReleaseVersion = '0.8.3'
$script:ExpectedGameHash = '72f2892607b0772470caae5db766dc07aaf636c3edfe26cefe019a5b9ddae56b'
$script:ProductNames = @('ChainedBeasts.ModFoundation','TermsAndContusions')
function Get-RepositoryRoot { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')) }
function Resolve-GameRoot([string]$GameRoot) {
    if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot=$env:CHAINED_BEASTS_GAME_DIR }
    if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot='C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts' }
    [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
}
function Assert-ChildPath([string]$Root,[string]$Path) {
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
    $resolved=[IO.Path]::GetFullPath($Path)
    if (!$resolved.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside expected root: $resolved" }
    $resolved
}
function Assert-GameClosed {
    if (@(Get-Process -Name ChainedBeasts -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Close Chained Beasts before deployment or rollback. The game was not terminated.' }
}
function Get-Sha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Assert-GameBuild([string]$GameRoot) {
    if ((Get-Sha (Join-Path $GameRoot 'GameAssembly.dll')) -ne $script:ExpectedGameHash) { throw 'GameAssembly differs from the inspected build. Reinspect before deploying.' }
    foreach($path in @('BepInEx/core/BepInEx.Core.dll','BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/interop/Assembly-CSharp.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $GameRoot $path) -PathType Leaf)) { throw "Missing installed loader/runtime reference: $path" }
    }
}
function Get-ProductFiles([string]$Configuration='Release') {
    $repo=Get-RepositoryRoot
    foreach($name in $script:ProductNames) {
        $path=Join-Path $repo "src/$name/bin/$Configuration/net6.0/$name.dll"
        $assembly=[Reflection.AssemblyName]::GetAssemblyName($path) # Metadata only; never load/execute the plugin.
        if ($assembly.Name -ne $name -or $assembly.Version.ToString(3) -ne $script:ReleaseVersion) { throw "Unexpected assembly identity/version: $path" }
        [pscustomobject]@{name=$name;path=$path;sha256=(Get-Sha $path);version=$script:ReleaseVersion}
    }
}
function Copy-Verified([string]$Source,[string]$Destination) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    if ((Get-Sha $Source) -ne (Get-Sha $Destination)) { throw "Copy verification failed: $Destination" }
}
function New-ModCheckpoint([string]$GameRoot,[string]$UserDataRoot,[string]$Label='deploy') {
    $repo=Get-RepositoryRoot
    $data=[IO.Path]::GetFullPath($UserDataRoot).TrimEnd('\','/')
    $game=[IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
    if ($data -eq $game -or $data.StartsWith($game+'\',[StringComparison]::OrdinalIgnoreCase) -or
        $game.StartsWith($data+'\',[StringComparison]::OrdinalIgnoreCase) -or $repo -eq $data -or
        $repo.StartsWith($data+'\',[StringComparison]::OrdinalIgnoreCase) -or $data -eq [IO.Path]::GetPathRoot($data).TrimEnd('\','/')) {
        throw 'User-data root overlaps game/source roots or is a drive root; checkpoint refused.'
    }
    $base=Join-Path $repo 'backups'
    New-Item -ItemType Directory -Force -Path $base | Out-Null
    $target=Assert-ChildPath $base (Join-Path $base ("expansion-stage7-$Label-"+(Get-Date -Format yyyyMMdd-HHmmss-fff)))
    New-Item -ItemType Directory -Path $target | Out-Null
    foreach($folder in @('src','docs','scripts','tests','examples')) {
        $sourceFolder=Join-Path $repo $folder
        # The maintainer-only tests folder is absent from public checkouts.
        if ($folder -eq 'tests' -and !(Test-Path -LiteralPath $sourceFolder -PathType Container)) { continue }
        Copy-Item -LiteralPath $sourceFolder -Destination $target -Recurse
    }
    foreach($file in @('README.md','LICENSE','.gitignore','THIRD_PARTY_NOTICES.md')) {
        if (Test-Path -LiteralPath (Join-Path $repo $file)) { Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $target }
    }
    foreach($pair in @(@('BepInEx/plugins','deployed-plugins'),@('BepInEx/config','loader-config'))) {
        $source=Join-Path $GameRoot $pair[0]
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $target $pair[1]) -Recurse }
        else { New-Item -ItemType Directory -Path (Join-Path $target $pair[1]) | Out-Null }
    }
    if (!(Test-Path -LiteralPath $UserDataRoot -PathType Container)) { throw 'User-data folder must exist before a migration checkpoint.' }
    Copy-Item -LiteralPath $UserDataRoot -Destination (Join-Path $target 'user-data') -Recurse
    $files=@(Get-ChildItem -LiteralPath $target -Recurse -File | ForEach-Object { @{path=[IO.Path]::GetRelativePath($target,$_.FullName);sha256=(Get-Sha $_.FullName)} })
    $files | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $target 'sha256-manifest.json') -Encoding utf8
    @{schemaVersion=1;gameRoot=$GameRoot;userDataRoot=$UserDataRoot;createdUtc=[DateTime]::UtcNow.ToString('o');sourceCommit=(git -C $repo rev-parse HEAD)} |
        ConvertTo-Json | Set-Content (Join-Path $target 'checkpoint.json') -Encoding utf8
    git -C $repo diff --binary | Set-Content (Join-Path $target 'tracked-working-tree.patch') -Encoding utf8
    $target
}
function Get-OwnedPluginFiles([string]$PluginRoot) {
    foreach($file in @(Get-ChildItem -LiteralPath $PluginRoot -Recurse -File -Filter '*.dll' -ErrorAction SilentlyContinue)) {
        $name=$null
        try { $name=[Reflection.AssemblyName]::GetAssemblyName($file.FullName).Name } catch { }
        if ($script:ProductNames -contains $name) { $file }
        elseif ($script:ProductNames -contains $file.BaseName) { throw "An unrelated/invalid assembly occupies a product filename: $($file.FullName)" }
    }
}
function Install-ModProducts([object[]]$Products,[string]$GameRoot,[string]$UserDataRoot) {
    Assert-GameClosed
    Assert-GameBuild $GameRoot
    $pluginRoot=Join-Path $GameRoot 'BepInEx/plugins'
    New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
    $old=@(Get-OwnedPluginFiles $pluginRoot)
    $backup=New-ModCheckpoint $GameRoot $UserDataRoot 'deploy'
    # Verify the current source DLLs are still the exact validated payload before changing the installation.
    foreach($product in $Products) { if ((Get-Sha $product.path) -ne $product.sha256) { throw 'Build payload changed after validation.' } }
    Assert-GameClosed
    try {
        foreach($file in $old) {
            $path=Assert-ChildPath $pluginRoot $file.FullName
            $saved=Join-Path (Join-Path $backup 'deployed-plugins') ([IO.Path]::GetRelativePath($pluginRoot,$path))
            if ((Get-Sha $path) -ne (Get-Sha $saved)) { throw 'Previous installed plugin was not backed up correctly.' }
            Remove-Item -LiteralPath $path # Exact backed-up owned DLL only, never a directory or third-party assembly.
        }
        # Explicit dependency order. BepInEx also enforces the declared hard version range at startup.
        foreach($name in $script:ProductNames) {
            $product=$Products | Where-Object name -eq $name
            Copy-Verified $product.path (Join-Path $pluginRoot ($name+'.dll'))
        }
        $installed=@(Get-OwnedPluginFiles $pluginRoot)
        if ($installed.Count -ne 2) { throw 'Duplicate/missing product assemblies after installation.' }
    }
    catch {
        foreach($name in $script:ProductNames) {
            $path=Assert-ChildPath $pluginRoot (Join-Path $pluginRoot ($name+'.dll'))
            if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}
        }
        foreach($file in $old) {
            $saved=Join-Path (Join-Path $backup 'deployed-plugins') ([IO.Path]::GetRelativePath($pluginRoot,$file.FullName))
            Copy-Verified $saved $file.FullName
        }
        throw
    }
    [pscustomobject]@{schemaVersion=1;version=$script:ReleaseVersion;deployedUtc=[DateTime]::UtcNow.ToString('o');gameRoot=$GameRoot;backupRoot=$backup;
        runtimeVerified=$false;gameLaunched=$false;products=@($Products | ForEach-Object { @{name=$_.name;path=(Join-Path $pluginRoot ($_.name+'.dll'));sha256=$_.sha256} })}
}
function Restore-ModCheckpoint([string]$BackupRoot,[string]$GameRoot,[string]$UserDataRoot) {
    Assert-GameClosed
    $backup=[IO.Path]::GetFullPath($BackupRoot)
    $metadata=Get-Content -Raw -LiteralPath (Join-Path $backup 'checkpoint.json') | ConvertFrom-Json
    if ($metadata.schemaVersion -ne 1 -or $metadata.gameRoot -ne $GameRoot -or $metadata.userDataRoot -ne $UserDataRoot) { throw 'Checkpoint roots/schema do not match the requested restore.' }
    $inventory=Get-Content -Raw -LiteralPath (Join-Path $backup 'sha256-manifest.json') | ConvertFrom-Json
    foreach($file in $inventory) {
        $path=Assert-ChildPath $backup (Join-Path $backup $file.path)
        if ((Get-Sha $path) -ne $file.sha256) { throw "Damaged checkpoint file: $path" }
    }
    foreach($required in @('deployed-plugins','loader-config','user-data')) { if(!(Test-Path -LiteralPath (Join-Path $backup $required) -PathType Container)){throw 'Incomplete checkpoint directories.'} }
    # Backup the current matching set before reverting. Whole-folder restoration removes newer unmatched sidecars.
    $undo=New-ModCheckpoint $GameRoot $UserDataRoot 'before-rollback'
    $pluginRoot=Assert-ChildPath $GameRoot (Join-Path $GameRoot 'BepInEx/plugins')
    $configRoot=Assert-ChildPath $GameRoot (Join-Path $GameRoot 'BepInEx/config')
    $dataRoot=[IO.Path]::GetFullPath($UserDataRoot).TrimEnd('\','/')
    $null=Assert-ChildPath ([IO.Path]::GetDirectoryName($dataRoot)) $dataRoot
    # Validate every resolved named target before ANY recursive removal. Never cross shells.
    foreach($path in @($pluginRoot,$configRoot,$dataRoot)) {
        if ($path -eq $backup -or $backup.StartsWith($path+'\',[StringComparison]::OrdinalIgnoreCase) -or $path -eq (Get-RepositoryRoot)) { throw 'Unsafe restore target.' }
    }
    foreach($path in @($pluginRoot,$configRoot,$dataRoot)) { if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Recurse -Force} }
    Copy-Item -LiteralPath (Join-Path $backup 'deployed-plugins') -Destination $pluginRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $backup 'loader-config') -Destination $configRoot -Recurse
    Copy-Item -LiteralPath (Join-Path $backup 'user-data') -Destination $dataRoot -Recurse
    Write-Output "Restored paired plugins/configuration/user data. Undo checkpoint: $undo"
}
