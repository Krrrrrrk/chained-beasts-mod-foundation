[CmdletBinding()]
param([Parameter(Mandatory)][string]$BackupRoot,[string]$GameRoot,
    [string]$UserDataRoot=(Join-Path $env:USERPROFILE 'AppData\LocalLow\Featherweight Games\ChainedBeasts'),[switch]$Restore)
$ErrorActionPreference='Stop'
if(!$Restore){throw 'Rollback requires explicit -Restore. Read docs/INSTALLATION.md and select a matching full checkpoint.'}
. (Join-Path $PSScriptRoot 'ReleaseWorkflow.ps1')
Restore-ModCheckpoint ([IO.Path]::GetFullPath($BackupRoot)) (Resolve-GameRoot $GameRoot) ([IO.Path]::GetFullPath($UserDataRoot).TrimEnd('\','/'))
