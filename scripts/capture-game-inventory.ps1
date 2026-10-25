[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-zA-Z0-9._-]+$')]
    [string]$Label,

    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts',

    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\analysis\phase2')
)

$ErrorActionPreference = 'Stop'
$resolvedGameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
[System.IO.Directory]::CreateDirectory($resolvedOutputRoot) | Out-Null

$files = foreach ($file in Get-ChildItem -LiteralPath $resolvedGameRoot -Recurse -File | Sort-Object FullName) {
    $relativePath = [System.IO.Path]::GetRelativePath($resolvedGameRoot, $file.FullName)
    [ordered]@{
        relativePath = $relativePath
        size = $file.Length
        modifiedUtc = $file.LastWriteTimeUtc.ToString('o')
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$inventory = [ordered]@{
    schemaVersion = 1
    label = $Label
    capturedAtUtc = [DateTime]::UtcNow.ToString('o')
    gameRoot = $resolvedGameRoot
    fileCount = @($files).Count
    files = @($files)
}

$outputPath = Join-Path $resolvedOutputRoot "$Label.json"
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output "Captured $($inventory.fileCount) files in $outputPath"
