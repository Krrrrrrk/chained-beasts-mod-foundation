# Windows PowerShell 5.1. Restores installer changes; never launches the game.
[CmdletBinding()]
param([string]$GameRoot)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
try {
    . (Join-Path $PSScriptRoot 'InstallerCore.ps1')
    . (Join-Path $PSScriptRoot 'UninstallerCore.ps1')
    Assert-GameClosed
    $bundle = Read-Bundle $PSScriptRoot
    $backupBase = Join-Path $env:LOCALAPPDATA 'ChainedBeastsModInstaller\Backups'
    $records = @(Get-BundleInstallRecords $backupBase $bundle.version)
    if ($records.Count -eq 0) { throw 'No active installation record was found for this bundle and Windows account. Nothing removed. Keep the installer backup folders; manual installations need manual removal.' }
    if ([string]::IsNullOrWhiteSpace($GameRoot)) {
        $games = @($records.Record.gameRoot | Select-Object -Unique)
        if ($games.Count -eq 1) { $GameRoot = $games[0] }
        else {
            Write-Host 'Select the game folder whose bundle installation you want to undo.'
            Add-Type -AssemblyName System.Windows.Forms
            $picker = New-Object System.Windows.Forms.FolderBrowserDialog
            $picker.Description = 'Select Chained Beasts to uninstall this mod bundle'
            $picker.ShowNewFolderButton = $false
            if ($picker.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { throw 'No folder selected. Nothing removed.' }
            $GameRoot = $picker.SelectedPath
            $picker.Dispose()
        }
    }
    $dataRoot = Join-Path $env:USERPROFILE 'AppData\LocalLow\Featherweight Games\ChainedBeasts'
    $plan = Get-BundleUninstallPlan $PSScriptRoot $GameRoot $backupBase
    Write-Host "Game folder: $($plan.GameRoot)"
    Write-Host 'This undoes this bundle installation, including reinstalls. Previous mod files are restored if they were replaced.'
    Write-Host $plan.LoaderMessage
    Write-Host 'Saves, settings, logs and generated interop files stay in place. A fresh backup is made first.'
    Write-Host ''
    Write-Host 'Finish or abandon any run owning custom content BEFORE uninstalling, or that run may not load afterward.' -ForegroundColor Yellow
    Write-Host 'To continue an older pre-mod run instead, restore its complete matching save/config backup manually.'
    $answer = Read-Host 'Type UNINSTALL to continue, or press Enter to cancel'
    if ($answer -cne 'UNINSTALL') { Write-Host 'Cancelled. Nothing removed.'; exit 0 }
    $result = Invoke-BundleUninstall $PSScriptRoot $GameRoot $dataRoot $backupBase
    Write-Host ''
    Write-Host 'UNINSTALLED: this bundle installation was undone.' -ForegroundColor Green
    Write-Host $result.LoaderMessage
    Write-Host "Pre-uninstall backup and file list: $($result.BackupRoot)"
    Write-Host 'Your current saves and settings were preserved. Do not resume a run that still owns removed content.'
    exit 0
} catch {
    Write-Host ''
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host 'If Windows denied access, close the game and right-click UNINSTALL.bat > Run as administrator using your own account.'
    exit 1
}
