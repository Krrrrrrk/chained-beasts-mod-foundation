[CmdletBinding()]
param([string]$GameRoot,[ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ReleaseWorkflow.ps1')
$realGame=Resolve-GameRoot $GameRoot
$repo=Get-RepositoryRoot
$script:passed=0
function Check([bool]$Value,[string]$Name){if(!$Value){throw "FAIL: $Name"};$script:passed++;Write-Output "PASS: $Name"}
function Reject([scriptblock]$Callback,[string]$Name){$rejected=$false;try{& $Callback | Out-Null}catch{$rejected=$true};Check $rejected $Name}
$testRoot=Join-Path $repo ('analysis/expansion-stage7/simulation-'+[Guid]::NewGuid().ToString('N'))
$fakeGame=Join-Path $testRoot 'game';$fakeData=Join-Path $testRoot 'userdata'
foreach($dir in @('BepInEx/core','BepInEx/interop','BepInEx/plugins/nested','BepInEx/config')){New-Item -ItemType Directory -Force -Path (Join-Path $fakeGame $dir)|Out-Null}
New-Item -ItemType Directory -Path $fakeData|Out-Null
# Real bytes, copied to a synthetic tree. Never load assemblies or alter the real installation.
foreach($file in @('GameAssembly.dll','BepInEx/core/BepInEx.Core.dll','BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/interop/Assembly-CSharp.dll')){Copy-Verified (Join-Path $realGame $file) (Join-Path $fakeGame $file)}
$baseline=Get-Content -Raw (Join-Path $repo 'analysis/expansion-stage7/baseline-path.txt')
$old=Join-Path $baseline.Trim() 'deployed-plugins/ChainedBeasts.ModFoundation.dll'
$oldPath=Join-Path $fakeGame 'BepInEx/plugins/nested/renamed-old.dll'
Copy-Verified $old $oldPath
$foreign=Join-Path $fakeGame 'BepInEx/plugins/unrelated-example.dll'
Copy-Verified (Join-Path $repo "examples/StandaloneContentExample/bin/$Configuration/net6.0/StandaloneContentExample.dll") $foreign
Set-Content (Join-Path $fakeData 'SaveData') 'synthetic-native-old'
Set-Content (Join-Path $fakeGame 'BepInEx/config/legacy.cfg') 'synthetic-config-old'
$beforeData=Get-Sha (Join-Path $fakeData 'SaveData');$beforeConfig=Get-Sha (Join-Path $fakeGame 'BepInEx/config/legacy.cfg');$beforeForeign=Get-Sha $foreign
Reject {Assert-ChildPath $fakeData (Join-Path $fakeData '../escaped')} 'path traversal rejected before filesystem mutation'
Reject {New-ModCheckpoint $fakeGame $fakeGame} 'overlapping game/user-data roots refused before checkpoint copy'
Reject {& (Join-Path $PSScriptRoot 'restore-plugin-backup.ps1') -BackupRoot $testRoot -GameRoot $fakeGame -UserDataRoot $fakeData} 'rollback without explicit switch refuses'
$products=@(Get-ProductFiles $Configuration)
$installed=Install-ModProducts $products $fakeGame $fakeData
Check (!(Test-Path -LiteralPath $oldPath) -and @(Get-OwnedPluginFiles (Join-Path $fakeGame 'BepInEx/plugins')).Count -eq 2) 'renamed nested monolithic DLL removed, exactly two new identities installed'
foreach($product in $products){Check ((Get-Sha (Join-Path $fakeGame ('BepInEx/plugins/'+$product.name+'.dll'))) -eq $product.sha256) ('deployed hash '+$product.name)}
Check ((Get-Sha $foreign) -eq $beforeForeign -and (Get-Sha (Join-Path $fakeData 'SaveData')) -eq $beforeData -and (Get-Sha (Join-Path $fakeGame 'BepInEx/config/legacy.cfg')) -eq $beforeConfig) 'unrelated plugin/config/save unchanged by deployment'
Check (!$installed.runtimeVerified -and !$installed.gameLaunched -and (Test-Path -LiteralPath (Join-Path $installed.backupRoot 'checkpoint.json'))) 'backup and honest deployment metadata recorded'
New-Item -ItemType Directory -Force -Path (Join-Path $fakeData 'ModFoundation/checkpoints')|Out-Null
Set-Content (Join-Path $fakeData 'ModFoundation/checkpoints/new.json') 'synthetic-new-sidecar'
Set-Content (Join-Path $fakeGame 'BepInEx/config/new.cfg') 'synthetic-new-config'
Restore-ModCheckpoint $installed.backupRoot $fakeGame $fakeData | Out-Null
Check ((Get-Sha $oldPath) -eq (Get-Sha $old) -and @(Get-OwnedPluginFiles (Join-Path $fakeGame 'BepInEx/plugins')).Count -eq 1) 'paired rollback restores old assembly identity/location'
Check (!(Test-Path -LiteralPath (Join-Path $fakeData 'ModFoundation')) -and !(Test-Path -LiteralPath (Join-Path $fakeGame 'BepInEx/config/new.cfg')) -and (Get-Sha (Join-Path $fakeData 'SaveData')) -eq $beforeData) 'whole paired restore removes newer unmatched sidecars/config and restores native bytes'
Check ((Get-Sha $foreign) -eq $beforeForeign) 'paired rollback restores unrelated saved plugin bytes'
$copyOriginal=(Get-Command Copy-Verified).ScriptBlock
try {
    function Copy-Verified([string]$Source,[string]$Destination){if($Destination.EndsWith('TermsAndContusions.dll')){throw 'Injected copy failure'};& $copyOriginal $Source $Destination}
    Reject {Install-ModProducts $products $fakeGame $fakeData} 'partial installation failure is surfaced'
} finally {Set-Item Function:\Copy-Verified -Value $copyOriginal}
Check ((Get-Sha $oldPath) -eq (Get-Sha $old) -and !(Test-Path -LiteralPath (Join-Path $fakeGame 'BepInEx/plugins/ChainedBeasts.ModFoundation.dll'))) 'partial-copy failure rolls back new DLL and restores old nested plugin'
Set-Content (Join-Path $installed.backupRoot 'user-data/SaveData') 'damaged-checkpoint'
Reject {Restore-ModCheckpoint $installed.backupRoot $fakeGame $fakeData} 'damaged checkpoint refused before restore'
Check ((Get-Sha (Join-Path $fakeData 'SaveData')) -eq $beforeData) 'failed restore preserves existing native data'
$packages=Join-Path $testRoot 'packages'
& (Join-Path $PSScriptRoot 'package-plugins.ps1') -OutputRoot $packages -SkipBuild -Configuration $Configuration | Out-Null
foreach($archive in Get-ChildItem -LiteralPath $packages -Filter '*.zip'){
    $zip=[IO.Compression.ZipFile]::OpenRead($archive.FullName)
    try {
        $entry=$zip.GetEntry('package-manifest.json');$reader=[IO.StreamReader]::new($entry.Open());try{$manifest=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
        $names=@($zip.Entries | Where-Object {$_.Name} | ForEach-Object {$_.FullName.Replace('\','/')})
        $expected=@($manifest.files | ForEach-Object path)+@('package-manifest.json')
        Check (@(Compare-Object ($names|Sort-Object) ($expected|Sort-Object)).Count -eq 0 -and @($names | Where-Object {$_ -match '(^/|\.\.|^[A-Za-z]:)'}).Count -eq 0) ('archive exact allowlist/no traversal '+$archive.Name)
        foreach($file in $manifest.files){$stream=$zip.GetEntry($file.path).Open();$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose();$stream.Dispose()};Check ($hash -eq $file.sha256) ('archive member hash '+$file.path)}
        $dlls=@($names|Where-Object {$_ -like '*.dll'})
        Check (@($dlls|Where-Object {$_ -notin @('BepInEx/plugins/ChainedBeasts.ModFoundation.dll','BepInEx/plugins/TermsAndContusions.dll')}).Count -eq 0 -and !$manifest.runtimeVerified -and !$manifest.published) ('no dependencies/examples bundled; runtime unverified '+$archive.Name)
    }finally{$zip.Dispose()}
}
Write-Output "Release workflow: $script:passed synthetic deployment/rollback/archive checks passed. Real game/user data untouched."
