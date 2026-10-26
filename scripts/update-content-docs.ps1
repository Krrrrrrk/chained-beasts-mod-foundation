[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& dotnet run --project (Join-Path $repo 'tests/Stage7Checks/Stage7Checks.csproj') -c Release -- $repo --write-docs
if($LASTEXITCODE -ne 0){throw 'Content documentation generation failed'}
