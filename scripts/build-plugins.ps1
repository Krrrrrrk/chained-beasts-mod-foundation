[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GameRoot,
    [switch]$FoundationOnly
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot = $env:CHAINED_BEASTS_GAME_DIR }
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts'
}
$GameRoot = [System.IO.Path]::GetFullPath($GameRoot)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bepInExRoot = Join-Path $GameRoot 'BepInEx'
$requiredReferences = @(
    'core\BepInEx.Core.dll', 'core\BepInEx.Unity.IL2CPP.dll', 'core\0Harmony.dll',
    'core\Il2CppInterop.Runtime.dll', 'interop\Assembly-CSharp.dll', 'interop\Il2Cppmscorlib.dll',
    'interop\UnityEngine.CoreModule.dll', 'interop\UnityEngine.IMGUIModule.dll',
    'interop\Unity.TextMeshPro.dll', 'interop\Unity.InputSystem.dll', 'interop\Unity.Netcode.Runtime.dll',
    'interop\Newtonsoft.Json.dll', 'interop\Unity.Collections.dll'
)
foreach ($reference in $requiredReferences) {
    $referencePath = Join-Path $bepInExRoot $reference
    if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
        throw "Missing installed runtime reference: $referencePath"
    }
}

# A source build is safe with the game open. This script never copies to BepInEx.
$projects = @('ChainedBeasts.ModFoundation')
if (-not $FoundationOnly) { $projects += 'TermsAndContusions' }
foreach ($projectName in $projects) {
    $projectPath = Join-Path $repositoryRoot "src\$projectName\$projectName.csproj"
    & dotnet build $projectPath --configuration $Configuration --nologo "-p:ChainedBeastsGameDir=$GameRoot"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $projectName ($LASTEXITCODE)" }
    $assemblyPath = Join-Path $repositoryRoot "src\$projectName\bin\$Configuration\net6.0\$projectName.dll"
    Write-Output "Built: $assemblyPath"
    Write-Output ('SHA256: ' + (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash.ToLowerInvariant())
}
Write-Output 'Build only. No installation changes; deployment requires explicit -Deploy.'
