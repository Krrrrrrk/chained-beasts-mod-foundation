[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release',[string]$GameRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ReleaseWorkflow.ps1')
$GameRoot=Resolve-GameRoot $GameRoot
$repo=Get-RepositoryRoot
& (Join-Path $PSScriptRoot 'test-stage6.ps1') -Configuration $Configuration -GameRoot $GameRoot
& dotnet build (Join-Path $repo 'examples/CategoryExamples/CategoryExamples.csproj') -c $Configuration --nologo "-p:ChainedBeastsGameDir=$GameRoot"
if($LASTEXITCODE -ne 0){throw 'Category examples build failed'}
& dotnet run --project (Join-Path $repo 'tests/Stage7Checks/Stage7Checks.csproj') -c $Configuration -- $repo $GameRoot $Configuration
if($LASTEXITCODE -ne 0){throw 'Stage 7 audit failed'}
& (Join-Path $PSScriptRoot 'test-release-workflow.ps1') -GameRoot $GameRoot -Configuration $Configuration
