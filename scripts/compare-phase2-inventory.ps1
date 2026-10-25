[CmdletBinding()]
param(
    [string]$Before = (Join-Path $PSScriptRoot '..\analysis\phase2\preinstall-game.json'),
    [string]$After = (Join-Path $PSScriptRoot '..\analysis\phase2\final-game.json'),
    [string]$Output = (Join-Path $PSScriptRoot '..\inventory\phase2-install-manifest.json')
)

$ErrorActionPreference = 'Stop'
$beforeInventory = Get-Content -LiteralPath $Before -Raw | ConvertFrom-Json
$afterInventory = Get-Content -LiteralPath $After -Raw | ConvertFrom-Json

$beforeByPath = @{}
foreach ($file in $beforeInventory.files) {
    $beforeByPath[$file.relativePath] = $file
}

$afterByPath = @{}
foreach ($file in $afterInventory.files) {
    $afterByPath[$file.relativePath] = $file
}

$added = foreach ($path in $afterByPath.Keys | Sort-Object) {
    if (-not $beforeByPath.ContainsKey($path)) {
        $afterByPath[$path]
    }
}

$removed = foreach ($path in $beforeByPath.Keys | Sort-Object) {
    if (-not $afterByPath.ContainsKey($path)) {
        $beforeByPath[$path]
    }
}

$modified = foreach ($path in $beforeByPath.Keys | Sort-Object) {
    if ($afterByPath.ContainsKey($path) -and $beforeByPath[$path].sha256 -ne $afterByPath[$path].sha256) {
        [ordered]@{
            relativePath = $path
            beforeSize = $beforeByPath[$path].size
            beforeSha256 = $beforeByPath[$path].sha256
            afterSize = $afterByPath[$path].size
            afterSha256 = $afterByPath[$path].sha256
        }
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    comparison = [ordered]@{
        before = $beforeInventory.label
        after = $afterInventory.label
        beforeFileCount = $beforeInventory.fileCount
        afterFileCount = $afterInventory.fileCount
    }
    summary = [ordered]@{
        added = @($added).Count
        removed = @($removed).Count
        modified = @($modified).Count
    }
    added = @($added)
    removed = @($removed)
    modified = @($modified)
}

$outputDirectory = Split-Path -Parent $Output
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Output -Encoding utf8
Write-Output "Added: $($manifest.summary.added)"
Write-Output "Removed: $($manifest.summary.removed)"
Write-Output "Modified original files: $($manifest.summary.modified)"
Write-Output "Manifest: $([System.IO.Path]::GetFullPath($Output))"
