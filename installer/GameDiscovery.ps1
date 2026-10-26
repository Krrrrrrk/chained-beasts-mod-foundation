# Read-only discovery. Never executes a candidate and never follows junctions.
Set-StrictMode -Version Latest
function Get-KnownSteamRoots {
    @("${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam")
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam')) {
        $reg = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($reg) {
            foreach ($name in @('SteamPath','InstallPath')) {
                if ($reg.PSObject.Properties[$name] -and $reg.$name) { [string]$reg.$name }
            }
        }
    }
}
function Get-LocalGameDrives {
    foreach ($drive in [IO.DriveInfo]::GetDrives()) {
        if ($drive.DriveType -eq [IO.DriveType]::Fixed -and $drive.IsReady) { $drive.RootDirectory.FullName }
    }
}
function Test-GameCandidate([string]$Path, $Bundle, [hashtable]$Seen) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    if ($Seen.ContainsKey($full)) { return }
    $Seen[$full] = $true
    if (!(Test-Path -LiteralPath (Join-Path $full 'ChainedBeasts.exe') -PathType Leaf)) { return }
    Write-Host "Checking game build: $full"
    try { Assert-SupportedGame $full $Bundle; $full }
    catch { Write-Host "Skipping incompatible or incomplete copy: $full" }
}
function Find-GameInstallation($Bundle, [ValidateRange(0,120)][int]$DeepSearchSeconds=35) {
    Write-Host 'Searching for Chained Beasts installation...'
    $seen = @{}
    $found = @()
    if ($env:CHAINED_BEASTS_GAME_DIR) { $found += @(Test-GameCandidate $env:CHAINED_BEASTS_GAME_DIR $Bundle $seen) }
    if ($found.Count -eq 1) { $found; return }
    $steam = @(Get-KnownSteamRoots | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $drives = @(Get-LocalGameDrives)
    foreach ($drive in $drives) {
        foreach ($relative in @('SteamLibrary','Steam','Games/Steam','Games/SteamLibrary','Program Files (x86)/Steam','Program Files/Steam')) {
            $steam += Join-Path $drive $relative
        }
        $found += @(Test-GameCandidate (Join-Path $drive 'Games/Chained Beasts') $Bundle $seen)
        $found += @(Test-GameCandidate (Join-Path $drive 'Chained Beasts') $Bundle $seen)
    }
    $libraries = @($steam)
    Write-Host 'Checking Steam libraries on local drives...'
    foreach ($root in @($steam | Select-Object -Unique)) {
        foreach ($relative in @('steamapps/libraryfolders.vdf','config/libraryfolders.vdf')) {
            $vdf = Join-Path $root $relative
            if (!(Test-Path -LiteralPath $vdf -PathType Leaf)) { continue }
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"(?:path|[0-9]+)"\s+"([^"]+)"')) {
                $candidate = $match.Groups[1].Value.Replace('\\','\')
                if ([IO.Path]::IsPathRooted($candidate)) { $libraries += $candidate }
            }
        }
    }
    foreach ($root in @($libraries | Select-Object -Unique)) {
        $apps = Join-Path $root 'steamapps'
        $found += @(Test-GameCandidate (Join-Path $apps 'common/Chained Beasts') $Bundle $seen)
        if (!(Test-Path -LiteralPath $apps -PathType Container)) { continue }
        # The manifest's install directory can differ from the visible game name.
        foreach ($manifest in @(Get-ChildItem -LiteralPath $apps -Filter 'appmanifest_*.acf' -File -ErrorAction SilentlyContinue)) {
            $text = Get-Content -LiteralPath $manifest.FullName -Raw
            if ($text -notmatch '(?i)"name"\s+"Chained\s*Beasts"') { continue }
            $match = [regex]::Match($text,'(?i)"installdir"\s+"([^"]+)"')
            if ($match.Success) {
                try { $candidate = Child-Path (Join-Path $apps 'common') $match.Groups[1].Value }
                catch { continue }
                $found += @(Test-GameCandidate $candidate $Bundle $seen)
            }
        }
    }
    if ($found.Count -gt 0) { $found | Select-Object -Unique; return }
    Write-Host 'No registered copy found. Searching folders on local drives...'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $queue = New-Object 'System.Collections.Generic.Queue[object]'
    foreach ($drive in $drives) { $queue.Enqueue([pscustomobject]@{Path=$drive;Depth=0}) }
    $walked = @{}
    $count = 0
    try {
        while ($queue.Count -gt 0 -and $timer.Elapsed.TotalSeconds -lt $DeepSearchSeconds -and $count -lt 20000) {
            $item = $queue.Dequeue()
            if ($walked.ContainsKey($item.Path)) { continue }
            $walked[$item.Path] = $true
            $count++
            if ($count % 100 -eq 1) {
                Write-Progress -Activity 'Searching for Chained Beasts installation' -Status "$count folders checked: $($item.Path)"
            }
            $found += @(Test-GameCandidate $item.Path $Bundle $seen)
            if ($found.Count -gt 0) { break }
            if ($item.Depth -ge 8) { continue }
            foreach ($dir in @(Get-ChildItem -LiteralPath $item.Path -Directory -ErrorAction SilentlyContinue)) {
                if ($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
                if ($dir.Name -in @('Windows','AppData','ProgramData','$Recycle.Bin','System Volume Information','.git','.codex','node_modules','BepInEx','dotnet','interop','backups','backup','artifacts','tests')) { continue }
                $queue.Enqueue([pscustomobject]@{Path=$dir.FullName;Depth=$item.Depth+1})
            }
        }
    } finally { Write-Progress -Activity 'Searching for Chained Beasts installation' -Completed }
    if ($found.Count -eq 0) { Write-Host "Automatic search finished ($count folders checked)." }
    $found | Select-Object -Unique
}
