[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$GameRoot
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot = $env:CHAINED_BEASTS_GAME_DIR }
if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts' }
# Includes both production builds, config migration/metadata checks, and the deployment refusal check.
& (Join-Path $PSScriptRoot 'test-stage1.ps1') -Configuration $Configuration -GameRoot $GameRoot
& dotnet build (Join-Path $repositoryRoot 'examples\StandaloneContentExample\StandaloneContentExample.csproj') --configuration $Configuration --nologo "-p:ChainedBeastsGameDir=$GameRoot"
if ($LASTEXITCODE -ne 0) { throw 'Standalone example build failed' }
& dotnet run --project (Join-Path $repositoryRoot 'tests\Stage2Checks\Stage2Checks.csproj') --configuration $Configuration -- $repositoryRoot $Configuration $GameRoot
if ($LASTEXITCODE -ne 0) { throw 'Stage 2 production-logic checks failed' }
