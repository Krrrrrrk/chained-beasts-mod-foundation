[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$GameRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = $env:CHAINED_BEASTS_GAME_DIR
}
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts'
}
$GameRoot = [System.IO.Path]::GetFullPath($GameRoot)
$project = Join-Path $PSScriptRoot '..\src\ChainedBeasts.ModFoundation\ChainedBeasts.ModFoundation.csproj'
$bepInExRoot = Join-Path $GameRoot 'BepInEx'
$pluginDirectory = Join-Path $bepInExRoot 'plugins'
$outputDirectory = Join-Path (Split-Path -Parent $project) "bin\$Configuration\net6.0"
$builtPlugin = Join-Path $outputDirectory 'ChainedBeasts.ModFoundation.dll'
$deployedPlugin = Join-Path $pluginDirectory 'ChainedBeasts.ModFoundation.dll'

if (Get-Process -Name 'ChainedBeasts' -ErrorAction SilentlyContinue) {
    throw 'Chained Beasts is running. Close it before building and deploying the plugin.'
}

$requiredReferences = @(
    (Join-Path $bepInExRoot 'core\BepInEx.Core.dll'),
    (Join-Path $bepInExRoot 'core\BepInEx.Unity.IL2CPP.dll'),
    (Join-Path $bepInExRoot 'core\0Harmony.dll'),
    (Join-Path $bepInExRoot 'core\Il2CppInterop.Runtime.dll'),
    (Join-Path $bepInExRoot 'interop\Assembly-CSharp.dll'),
    (Join-Path $bepInExRoot 'interop\Il2Cppmscorlib.dll'),
    (Join-Path $bepInExRoot 'interop\UnityEngine.CoreModule.dll'),
    (Join-Path $bepInExRoot 'interop\UnityEngine.IMGUIModule.dll'),
    (Join-Path $bepInExRoot 'interop\Unity.InputSystem.dll'),
    (Join-Path $bepInExRoot 'interop\Unity.Netcode.Runtime.dll')
)
foreach ($reference in $requiredReferences) {
    if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) {
        throw "Missing loader API reference: $reference"
    }
}

& dotnet build $project --configuration $Configuration --nologo "-p:ChainedBeastsGameDir=$GameRoot"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

[System.IO.Directory]::CreateDirectory($pluginDirectory) | Out-Null
Copy-Item -LiteralPath $builtPlugin -Destination $deployedPlugin -Force

$hash = (Get-FileHash -LiteralPath $deployedPlugin -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Built: $builtPlugin"
Write-Output "Deployed: $deployedPlugin"
Write-Output "SHA256: $hash"
