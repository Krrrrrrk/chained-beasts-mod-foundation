[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GameRoot,
    [string]$UserDataRoot = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Featherweight Games\ChainedBeasts'),
    [switch]$Deploy,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
# Deliberate opt-in: every build/check command remains unable to deploy accidentally.
if (!$Deploy) { throw 'Deployment requires explicit -Deploy. Use scripts/build-plugins.ps1 for build-only work.' }
. (Join-Path $PSScriptRoot 'ReleaseWorkflow.ps1')
$GameRoot=Resolve-GameRoot $GameRoot
$UserDataRoot=[IO.Path]::GetFullPath($UserDataRoot).TrimEnd('\','/')
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build-plugins.ps1') -Configuration $Configuration -GameRoot $GameRoot }
$products=@(Get-ProductFiles $Configuration)
$result=Install-ModProducts $products $GameRoot $UserDataRoot
$output=Join-Path (Get-RepositoryRoot) ('artifacts/packages/'+$script:ReleaseVersion)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'deployment.json') -Encoding utf8
$result | ConvertTo-Json -Depth 8
