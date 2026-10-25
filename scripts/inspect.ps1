[CmdletBinding()]
param(
    [string]$GameRoot,
    [string]$UserDataRoot,
    [string]$WorkspaceRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = $env:CHAINED_BEASTS_GAME_DIR
}
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts'
}
if ([string]::IsNullOrWhiteSpace($UserDataRoot)) {
    $UserDataRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\Featherweight Games\ChainedBeasts'
}
if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

function Assert-Directory([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label directory is unavailable: $Path"
    }
}

function Get-RelativePath([string]$Root, [string]$Path) {
    return [IO.Path]::GetRelativePath($Root, $Path)
}

function Get-PeInfo([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $stream.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset + 4
        $machine = $reader.ReadUInt16()
        $architecture = switch ($machine) {
            0x8664 { 'x64' }
            0x014c { 'x86' }
            0xaa64 { 'arm64' }
            default { 'unknown' }
        }
        $item = Get-Item -LiteralPath $Path
        return [ordered]@{
            architecture = $architecture
            machine = ('0x{0:X4}' -f $machine)
            fileVersion = $item.VersionInfo.FileVersion
            productVersion = $item.VersionInfo.ProductVersion
        }
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Get-GameClassification([string]$RelativePath) {
    $p = $RelativePath.Replace('/', '\')
    if ($p -match '^(ChainedBeasts\.exe|UnityPlayer\.dll|GameAssembly\.dll|baselib\.dll|UnityCrashHandler64\.exe)$') { return 'runtime-native' }
    if ($p -like 'ChainedBeasts_Data\il2cpp_data\*') { return 'il2cpp-code-metadata' }
    if ($p -like 'ChainedBeasts_Data\StreamingAssets\aa\*') { return 'addressables' }
    if ($p -like 'ChainedBeasts_Data\StreamingAssets\*') { return 'streaming-assets' }
    if ($p -like 'ChainedBeasts_Data\Plugins\*') { return 'native-plugin' }
    if ($p -match '\.(assets|resS|resource)$' -or $p -like 'ChainedBeasts_Data\level*' -or $p -like 'ChainedBeasts_Data\globalgamemanagers*') { return 'unity-serialized-asset' }
    if ($p -match '(app\.info|boot\.config|ScriptingAssemblies\.json|RuntimeInitializeOnLoads\.json)$') { return 'build-metadata' }
    return 'runtime-other'
}

function Get-UserClassification([string]$RelativePath) {
    $p = $RelativePath.Replace('/', '\')
    if ($p -match '^Player(-prev)?\.log$') { return 'log' }
    if ($p -match '^(SaveData|SaveDataPrivate|persistent_data(_private)?\.json)$') { return 'save-data' }
    if ($p -match '^(Settings|settings\.json|bindings\.json)$') { return 'settings' }
    if ($p -match '^RemoteConfig') { return 'remote-config-cache' }
    if ($p -eq 'steam_autocloud.vdf') { return 'steam-cloud-metadata' }
    if ($p -like 'Unity\*') { return 'unity-analytics-cache' }
    return 'user-data-other'
}

function Get-JsonSummary([string]$Path) {
    try {
        $json = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -Depth 100
        $keys = @()
        if ($json -is [System.Management.Automation.PSCustomObject]) {
            $keys = @($json.PSObject.Properties.Name)
        }
        return [ordered]@{
            format = 'json-text'
            valid = $true
            topLevelKeys = $keys
        }
    }
    catch {
        return [ordered]@{
            format = 'text-or-unrecognized'
            valid = $false
            topLevelKeys = @()
        }
    }
}

function Get-UserFormat([IO.FileInfo]$File) {
    if ($File.Name -match '^Player(-prev)?\.log$') {
        return [ordered]@{ format = 'unity-log-text'; valid = $true; topLevelKeys = @() }
    }
    if ($File.Name -eq 'steam_autocloud.vdf') {
        return [ordered]@{ format = 'valve-keyvalues-text'; valid = $true; topLevelKeys = @() }
    }
    if ($File.Length -le 5MB) {
        return Get-JsonSummary $File.FullName
    }
    return [ordered]@{ format = 'unrecognized-or-binary'; valid = $null; topLevelKeys = @() }
}

function Get-OptionalSha256([IO.FileInfo]$File, [bool]$Critical) {
    if ($Critical -or $File.Length -le 50MB) {
        return (Get-FileHash -Algorithm SHA256 -LiteralPath $File.FullName).Hash.ToLowerInvariant()
    }
    return $null
}

function Sanitize-LogLine([string]$Line) {
    if ($null -eq $Line) { return $null }
    $safe = $Line -replace '\b7656119\d{10}\b', '<redacted-steam-id>'
    $safe = $safe -replace '\b10\d{16}\b', '<redacted-lobby-id>'
    $safe = $safe -replace '(Join Code:\s*)\S+', '$1<redacted>'
    return $safe
}

Assert-Directory $GameRoot 'Game installation'
Assert-Directory $UserDataRoot 'User-data'
Assert-Directory $WorkspaceRoot 'Workspace'

$inventoryDirectory = Join-Path $WorkspaceRoot 'inventory'
$analysisDirectory = Join-Path $WorkspaceRoot 'analysis'
New-Item -ItemType Directory -Force -Path $inventoryDirectory, $analysisDirectory | Out-Null

$gameRootFull = [IO.Path]::GetFullPath($GameRoot)
$userRootFull = [IO.Path]::GetFullPath($UserDataRoot)

$criticalGamePaths = @(
    'ChainedBeasts.exe',
    'UnityPlayer.dll',
    'GameAssembly.dll',
    'ChainedBeasts_Data\globalgamemanagers',
    'ChainedBeasts_Data\boot.config',
    'ChainedBeasts_Data\ScriptingAssemblies.json',
    'ChainedBeasts_Data\il2cpp_data\Metadata\global-metadata.dat',
    'ChainedBeasts_Data\StreamingAssets\aa\catalog.bin',
    'ChainedBeasts_Data\StreamingAssets\aa\catalog.hash',
    'ChainedBeasts_Data\StreamingAssets\aa\settings.json'
)

$gameFiles = foreach ($file in (Get-ChildItem -Force -Recurse -File -LiteralPath $gameRootFull | Sort-Object FullName)) {
    $relative = Get-RelativePath $gameRootFull $file.FullName
    $critical = $criticalGamePaths -contains $relative
    $pe = $null
    if ($file.Extension -in @('.exe', '.dll')) {
        try { $pe = Get-PeInfo $file.FullName } catch { $pe = $null }
    }
    [ordered]@{
        source = 'game'
        relativePath = $relative
        classification = Get-GameClassification $relative
        size = $file.Length
        modified = $file.LastWriteTime.ToString('o')
        sha256 = Get-OptionalSha256 $file $critical
        pe = $pe
    }
}

$userFiles = foreach ($file in (Get-ChildItem -Force -Recurse -File -LiteralPath $userRootFull | Sort-Object FullName)) {
    $relative = Get-RelativePath $userRootFull $file.FullName
    $format = Get-UserFormat $file
    [ordered]@{
        source = 'user-data'
        relativePath = $relative
        classification = Get-UserClassification $relative
        size = $file.Length
        modified = $file.LastWriteTime.ToString('o')
        sha256 = if ($file.Length -le 10MB) { Get-OptionalSha256 $file $false } else { $null }
        format = $format.format
        jsonValid = $format.valid
        topLevelKeys = $format.topLevelKeys
        containsPotentialPersonalData = ($relative -match '^(Player|SaveData|persistent_data|Unity\\)')
    }
}

$dataRoot = Join-Path $gameRootFull 'ChainedBeasts_Data'
$exePath = Join-Path $gameRootFull 'ChainedBeasts.exe'
$metadataPath = Join-Path $dataRoot 'il2cpp_data\Metadata\global-metadata.dat'
$playerLogPath = Join-Path $userRootFull 'Player.log'

$engineVersion = $null
if (Test-Path -LiteralPath $playerLogPath) {
    $engineLine = Select-String -LiteralPath $playerLogPath -Pattern '^Initialize engine version:' | Select-Object -First 1
    if ($engineLine -and $engineLine.Line -match 'version:\s+([^\s]+)\s+\(([^)]+)\)') {
        $engineVersion = [ordered]@{ version = $Matches[1]; revision = $Matches[2]; evidence = 'Player.log' }
    }
}
if ($null -eq $engineVersion -and (Test-Path -LiteralPath $exePath)) {
    $engineVersion = [ordered]@{ version = (Get-Item -LiteralPath $exePath).VersionInfo.ProductVersion; revision = $null; evidence = 'ChainedBeasts.exe version resource' }
}

$metadataHeader = $null
if (Test-Path -LiteralPath $metadataPath) {
    $stream = [IO.File]::OpenRead($metadataPath)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $magic = $reader.ReadUInt32()
        $version = $reader.ReadInt32()
        $metadataHeader = [ordered]@{ magic = ('0x{0:X8}' -f $magic); version = $version }
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

$gameVersion = $null
$globalManagersPath = Join-Path $dataRoot 'globalgamemanagers'
if (Test-Path -LiteralPath $globalManagersPath) {
    $globalText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($globalManagersPath))
    $versionCandidates = @([regex]::Matches($globalText, '(?<!\d)\d+\.\d+\.\d+(?!\d)') | ForEach-Object Value | Select-Object -Unique)
    $gameVersion = $versionCandidates | Where-Object { $_ -notmatch '^6000\.' } | Select-Object -First 1
}

$steamManifest = $null
$steamApps = Split-Path -Parent (Split-Path -Parent $gameRootFull)
foreach ($manifest in (Get-ChildItem -File -LiteralPath $steamApps -Filter 'appmanifest_*.acf' -ErrorAction SilentlyContinue)) {
    $content = Get-Content -Raw -LiteralPath $manifest.FullName
    if ($content -notmatch '"installdir"\s+"Chained Beasts"') { continue }
    $appId = if ($content -match '"appid"\s+"(\d+)"') { $Matches[1] } else { $null }
    $buildId = if ($content -match '"buildid"\s+"(\d+)"') { $Matches[1] } else { $null }
    $depotManifest = if ($content -match '"manifest"\s+"(\d+)"') { $Matches[1] } else { $null }
    $steamManifest = [ordered]@{
        path = $manifest.FullName
        appId = $appId
        buildId = $buildId
        depotManifest = $depotManifest
    }
    break
}

$loaderPatterns = 'BepInEx|MelonLoader|doorstop|winhttp\.dll|version\.dll|dxgi\.dll|dinput8\.dll|ReiPatcher|IPA\.exe'
$loaderCandidates = @(Get-ChildItem -Force -Recurse -LiteralPath $gameRootFull | Where-Object {
    $_.Name -match $loaderPatterns
} | ForEach-Object { Get-RelativePath $gameRootFull $_.FullName })

$logEvidence = @()
foreach ($logName in @('Player.log', 'Player-prev.log')) {
    $logPath = Join-Path $userRootFull $logName
    if (-not (Test-Path -LiteralPath $logPath)) { continue }
    $startup = @(Select-String -LiteralPath $logPath -Pattern 'Initialize engine version|Steam platform initialized|Twitch SDK version|\[NetworkManagerEx\] Active transport|Unity\.Netcode|RemoteConfig' | Select-Object -First 40 | ForEach-Object {
        [ordered]@{ lineNumber = $_.LineNumber; line = Sanitize-LogLine $_.Line }
    })
    $errors = @(Select-String -LiteralPath $logPath -Pattern 'Exception|Error|Failed|NullReference' -CaseSensitive:$false |
        Group-Object Line | Sort-Object Count -Descending | Select-Object -First 20 | ForEach-Object {
            [ordered]@{ count = $_.Count; line = Sanitize-LogLine $_.Name }
        })
    $logEvidence += [ordered]@{
        file = $logName
        size = (Get-Item -LiteralPath $logPath).Length
        startupEvidence = $startup
        frequentErrorOrWarningLines = $errors
    }
}

$evidencePath = Join-Path $analysisDirectory 'log-evidence.json'
$logEvidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8

$inventory = [ordered]@{
    schemaVersion = 1
    generatedAt = (Get-Date).ToString('o')
    roots = [ordered]@{
        game = $gameRootFull
        userData = $userRootFull
        workspace = [IO.Path]::GetFullPath($WorkspaceRoot)
    }
    detectedBuild = [ordered]@{
        productVersion = $gameVersion
        engine = 'Unity'
        engineVersion = $engineVersion
        executable = if (Test-Path -LiteralPath $exePath) { Get-PeInfo $exePath } else { $null }
        steam = $steamManifest
        scriptingBackend = 'IL2CPP'
        il2cppMetadataHeader = $metadataHeader
        indicators = [ordered]@{
            gameAssembly = Test-Path -LiteralPath (Join-Path $gameRootFull 'GameAssembly.dll')
            il2cppData = Test-Path -LiteralPath (Join-Path $dataRoot 'il2cpp_data')
            globalMetadata = Test-Path -LiteralPath $metadataPath
            managedDirectory = Test-Path -LiteralPath (Join-Path $dataRoot 'Managed')
            assemblyCSharpOnDisk = Test-Path -LiteralPath (Join-Path $dataRoot 'Managed\Assembly-CSharp.dll')
            monoBleedingEdge = Test-Path -LiteralPath (Join-Path $gameRootFull 'MonoBleedingEdge')
        }
        existingLoaderCandidates = $loaderCandidates
    }
    hashPolicy = 'SHA-256 for critical game files and files <= 50 MiB; user files only when <= 10 MiB; null means intentionally skipped.'
    files = @($gameFiles) + @($userFiles)
    generatedEvidence = Get-RelativePath $WorkspaceRoot $evidencePath
}

$inventoryPath = Join-Path $inventoryDirectory 'relevant-files.json'
$inventory | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $inventoryPath -Encoding utf8

Write-Host "Wrote inventory: $inventoryPath"
Write-Host "Wrote sanitized log summary: $evidencePath"
Write-Host "Read-only sources were not modified."

