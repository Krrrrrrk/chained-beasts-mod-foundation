[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GameRoot
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot = $env:CHAINED_BEASTS_GAME_DIR }
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts'
}
$GameRoot = [System.IO.Path]::GetFullPath($GameRoot)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'build-plugins.ps1') -Configuration $Configuration -GameRoot $GameRoot
$checksProject = Join-Path $repositoryRoot 'tests\Stage1Checks\Stage1Checks.csproj'
& dotnet run --project $checksProject --configuration $Configuration "-p:ChainedBeastsGameDir=$GameRoot" -- $repositoryRoot $Configuration
if ($LASTEXITCODE -ne 0) { throw "Stage 1 nongame checks failed ($LASTEXITCODE)" }

$deploymentBlocked = $false
try {
    & (Join-Path $PSScriptRoot 'build-and-deploy-plugin.ps1') -Configuration $Configuration -GameRoot $GameRoot
}
catch {
    if ($_.Exception.Message -notlike 'Deployment requires explicit -Deploy.*') { throw }
    $deploymentBlocked = $true
}
if (-not $deploymentBlocked) { throw 'Intermediate-stage deployment guard did not block installation.' }
Write-Output 'PASS: deployment without explicit -Deploy is blocked before any installation changes'
