# Windows PowerShell 5.1, included with Windows. Never launches the game.
[CmdletBinding()]
param([string]$GameRoot, [string]$ExpectedUserSid)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
try {
    . (Join-Path $PSScriptRoot 'InstallerCore.ps1')
    . (Join-Path $PSScriptRoot 'GameDiscovery.ps1')
    if (![Environment]::Is64BitOperatingSystem) { throw 'This installer requires 64-bit Windows.' }
    if ($ExpectedUserSid -and [Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $ExpectedUserSid) { throw 'The elevated installer is running as a different Windows account. Nothing installed; use your own account so your correct saves are backed up.' }
    Assert-GameClosed
    $bundle = Read-Bundle $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($GameRoot)) {
        $found = @(Find-GameInstallation $bundle)
        if ($found.Count -eq 1) {
            Write-Host "Found Chained Beasts: $($found[0])"
            $GameRoot = $found[0]
        } else {
            Write-Host 'Automatic discovery needs a hand. Select the folder containing ChainedBeasts.exe.'
            Add-Type -AssemblyName System.Windows.Forms
            $picker = New-Object System.Windows.Forms.FolderBrowserDialog
            $picker.Description = 'Select your Chained Beasts game folder'
            $picker.ShowNewFolderButton = $false
            if ($picker.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { throw 'No folder selected. Nothing installed.' }
            $GameRoot = $picker.SelectedPath
            $picker.Dispose()
        }
    }
    $GameRoot = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
    Assert-SupportedGame $GameRoot $bundle
    if (!(Test-GameFolderWritable $GameRoot)) {
        $resultCode = Start-ElevatedInstall (Join-Path $PSScriptRoot 'Install.ps1') $GameRoot
        if ($resultCode -eq 0) {
            Write-Host 'INSTALLED: Terms & Contusions + Foundation 0.8.3 and Battle Numbers 0.1.1' -ForegroundColor Green
            Write-Host 'Start the game normally in Steam and choose Solo. BepInEx dependencies are already included.'
            Write-Host "Backups: $env:LOCALAPPDATA\ChainedBeastsModInstaller\Backups"
        }
        exit $resultCode
    }
    $dataRoot = Join-Path $env:USERPROFILE 'AppData\LocalLow\Featherweight Games\ChainedBeasts'
    $backupBase = Join-Path $env:LOCALAPPDATA 'ChainedBeastsModInstaller\Backups'
    Write-Host 'Checking files and backing up existing mods, settings and saves...'
    $result = Invoke-BundleInstall $PSScriptRoot $GameRoot $dataRoot $backupBase
    Write-Host ''
    Write-Host 'INSTALLED: Terms & Contusions + Foundation 0.8.3 and Battle Numbers 0.1.1' -ForegroundColor Green
    Write-Host "Backup and file list: $($result.BackupRoot)"
    Write-Host 'Start Chained Beasts normally through Steam and choose Solo.'
    Write-Host 'Everything BepInEx needs for this build is installed locally; no dependency download is needed.'
    Write-Host 'The first launch can still take several minutes while bindings are generated locally.'
    Write-Host 'Existing settings were kept. Fresh installs use normal offers and cheats start off.'
    Write-Host 'For problems, keep Player.log and BepInEx\LogOutput.log before relaunching.'
    exit 0
} catch {
    Write-Host ''
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host 'If Windows denied access, close the game and right-click INSTALL.bat > Run as administrator.'
    Write-Host 'Use your own Windows account so the correct save folder is backed up.'
    exit 1
}
