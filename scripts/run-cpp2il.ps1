[CmdletBinding()]
param(
    [string]$GameRoot,
    [string]$WorkspaceRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = $env:CHAINED_BEASTS_GAME_DIR
}
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Chained Beasts'
}
if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}

if (-not (Test-Path -LiteralPath $GameRoot -PathType Container)) {
    throw "Game directory is unavailable: $GameRoot"
}
if (-not (Test-Path -LiteralPath $WorkspaceRoot -PathType Container)) {
    throw "Workspace directory is unavailable: $WorkspaceRoot"
}

$cpp2il = Get-ChildItem -File -LiteralPath (Join-Path $WorkspaceRoot 'tools\Cpp2IL') -Filter 'Cpp2IL-*-Windows.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $cpp2il) {
    throw 'Cpp2IL was not found under tools\Cpp2IL. See MODDING_REPORT.md for the pinned official release and digest.'
}

$analysisRoot = Join-Path $WorkspaceRoot 'analysis\cpp2il'
$dummyDirectory = Join-Path $analysisRoot 'dummy-dlls'
$sourceDirectory = Join-Path $analysisRoot 'Assembly-CSharp'
$logPath = Join-Path $analysisRoot 'cpp2il.log'
New-Item -ItemType Directory -Force -Path $dummyDirectory | Out-Null

& $cpp2il.FullName `
    --game-path $GameRoot `
    --exe-name 'ChainedBeasts' `
    --output-as dll_empty `
    --output-to $dummyDirectory `
    --low-memory-mode `
    --verbose *>&1 | Tee-Object -LiteralPath $logPath

if ($LASTEXITCODE -ne 0) {
    throw "Cpp2IL failed with exit code $LASTEXITCODE. See $logPath"
}

$ilspy = Join-Path $WorkspaceRoot 'tools\ilspy\ilspycmd.exe'
$assemblyCSharp = Join-Path $dummyDirectory 'Assembly-CSharp.dll'
if ((Test-Path -LiteralPath $ilspy) -and (Test-Path -LiteralPath $assemblyCSharp)) {
    New-Item -ItemType Directory -Force -Path $sourceDirectory | Out-Null
    & $ilspy --disable-updatecheck --nested-directories -p -o $sourceDirectory -r $dummyDirectory $assemblyCSharp
    if ($LASTEXITCODE -ne 0) {
        throw "ILSpy failed with exit code $LASTEXITCODE"
    }
    Write-Host "Generated metadata-derived C# stubs: $sourceDirectory"
}
else {
    Write-Warning 'ILSpy was not found or Assembly-CSharp.dll was not generated; skipping C# stub output.'
}

Write-Host "Cpp2IL log: $logPath"
Write-Host 'Generated assemblies contain recovered metadata and placeholder/empty bodies, not the original managed implementations.'

