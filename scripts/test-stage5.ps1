[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release',[string]$GameRoot)
$ErrorActionPreference='Stop'
$repositoryRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if([string]::IsNullOrWhiteSpace($GameRoot)){$GameRoot=$env:CHAINED_BEASTS_GAME_DIR}
if([string]::IsNullOrWhiteSpace($GameRoot)){$GameRoot='C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts'}
& (Join-Path $PSScriptRoot 'test-stage4.ps1') -Configuration $Configuration -GameRoot $GameRoot
& dotnet run --project (Join-Path $repositoryRoot 'tests\Stage5Checks\Stage5Checks.csproj') --configuration $Configuration -- $repositoryRoot $GameRoot
if($LASTEXITCODE -ne 0){throw 'Stage 5 nongame checks failed'}
