[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release',[string]$GameRoot,[string]$OutputRoot,[switch]$SkipBuild,[switch]$IncludeBattleNumbers)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ReleaseWorkflow.ps1')
$repo=Get-RepositoryRoot
if(!$SkipBuild){& (Join-Path $PSScriptRoot 'build-plugins.ps1') -Configuration $Configuration -GameRoot (Resolve-GameRoot $GameRoot)}
$products=@(Get-ProductFiles $Configuration)
if($IncludeBattleNumbers){
    if(!$SkipBuild){
        & dotnet build (Join-Path $repo 'chained-beasts-battle-numbers/src/ChainedBeasts.BattleNumbers/ChainedBeasts.BattleNumbers.csproj') -c $Configuration --nologo "-p:ChainedBeastsGameDir=$(Resolve-GameRoot $GameRoot)"
        if($LASTEXITCODE -ne 0){throw 'Battle Numbers build failed.'}
    }
    $products+=Get-BattleNumbersProduct $Configuration
}
if([string]::IsNullOrWhiteSpace($OutputRoot)){$OutputRoot=Join-Path $repo ('artifacts/packages/'+$script:ReleaseVersion)}
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$packages=@(
    @{name='ChainedBeasts.ModFoundation';members=@('ChainedBeasts.ModFoundation')},
    @{name='TermsAndContusions';members=@('TermsAndContusions')},
    @{name='TermsAndContusions-with-Foundation';members=@('ChainedBeasts.ModFoundation','TermsAndContusions')})
$result=@()
if($IncludeBattleNumbers){$packages+=@{name='Chained-Beasts-Multiplayer-Test';members=@('ChainedBeasts.ModFoundation','TermsAndContusions','ChainedBeasts.BattleNumbers')}}
foreach($package in $packages){
    $stage=Join-Path $OutputRoot ('staging-'+$package.name+'-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    foreach($name in $package.members){$product=$products | Where-Object name -eq $name;Copy-Verified $product.path (Join-Path $stage "BepInEx/plugins/$name.dll")}
    Copy-Verified (Join-Path $repo 'LICENSE') (Join-Path $stage 'LICENSE')
    Copy-Verified (Join-Path $repo 'THIRD_PARTY_NOTICES.md') (Join-Path $stage 'THIRD_PARTY_NOTICES.md')
    Copy-Verified (Join-Path $repo 'docs/MULTIPLAYER.md') (Join-Path $stage 'MULTIPLAYER.md')
    Copy-Verified (Join-Path $repo 'docs/CONTENT_SELECTION.md') (Join-Path $stage 'CONTENT_SELECTION.md')
    Copy-Verified (Join-Path $repo 'docs/ACTIVATION_FEEDBACK.md') (Join-Path $stage 'ACTIVATION_FEEDBACK.md')
    Copy-Verified (Join-Path $repo 'docs/GAME_UPDATE_1.0.2.md') (Join-Path $stage 'GAME_UPDATE_1.0.2.md')
    if($package.members -contains 'ChainedBeasts.BattleNumbers'){Copy-Verified (Join-Path $repo 'chained-beasts-battle-numbers/LICENSE') (Join-Path $stage 'BattleNumbers-LICENSE.txt')}
    $members=$package.members -join ', '
    @"
# $($package.name) $script:ReleaseVersion — local build

Included DLLs: $members. This archive does not install or contain BepInEx.
TermsAndContusions requires ChainedBeasts.ModFoundation >=1.0.0 <2.0.0.
The optional combined multiplayer test ZIP also includes Battle Numbers 0.2.x;
see package-manifest.json for its exact version and hash.
Foundation alone is supported. Optional teaching examples are not included.
0.9.8 adds the main-menu TC Content panel. All content defaults enabled.
1.0.0 promotes the 0.9.9 build: styled TC Content cards, category counts,
clear save state and filtering of direct random training injuries. Search is
currently unavailable; category and page controls remain usable.
Choose entries and Save before creating/joining a party or starting Solo;
no restart is needed. The host's offer selection is applied temporarily on
clients before the party can start. Their saved personal preferences remain
unchanged and return on disconnect. Selection is locked while connected.
Existing owned content and saved offers are preserved; filters affect newly
generated offers. Matching mod versions and other gameplay settings/prices
are still required. This menu/profile protocol awaits manual game testing.
0.9.7 adds Iron Grip (one rank): hits no longer make you drop carried enemies.
It is enabled by default through [Skills] EnableIronGrip in
BepInEx/config/local.chainedbeasts.termsandcontusions.cfg. Normal throws,
releases and cleanup still apply. Iron Grip worked in a manual test; broader
interaction and matching-party multiplayer coverage still needs checking.

1. Close the game. Back up all BepInEx/plugins, BepInEx/config and the complete
   LocalLow/Featherweight Games/ChainedBeasts user-data folder, including any
   ModFoundation/checkpoints sidecars, together before changing versions.
2. Use the inspected Steam Windows x64 game 1.0.2 / build 26231777 / Unity 6000.3.21f1 with
   BepInEx 6.0.0-be.791+b782da4 Unity IL2CPP x64 and generated runtime interop.
3. Remove prior copies of our DLLs from all BepInEx plugin subfolders after
   backing them up. The old content-bearing monolithic Foundation must not
   coexist with the new split. Extract this archive's BepInEx folder into the
   game root. For the content-only archive also install matching Foundation.
4. Keep existing configuration. First startup migrates the eight old content
   settings into the pack only where no explicit destination value exists.
   Fresh forced offers/cheats default off; existing force choices are preserved.
5. Read MULTIPLAYER.md before online testing: every player needs matching gameplay
   DLLs/settings. Everyone must join before starting or resuming; no mid-run
   joining/reconnect or host migration. F1 mutations remain Solo-only.
   Check BepInEx/LogOutput.log for both $script:ReleaseVersion
   plugins, successful shared initialization and no registration errors. The
   release has passed nongame checks; broader Solo and host/client gameplay
   testing is still needed. No attempt at balance was made.

Native SaveData and its exact matching sidecars must be copied/restored together.
Offer toggles preserve owned content. Do not remove required content plugins
and resume a run that owns their content. Old v0.7.3 cannot
read the new checkpoint protocol: downgrade DLL/config/user-data as a matching
pre-expansion set, not just the DLL. Multiplayer is experimental; the new skill's
network and edge-case coverage remain incomplete.
Automatic Steam Cloud sidecar sync, original icons and full arena-instance
restoration are not supported.
New text uses English fallback; surgery reroll remains deferred.

Full installation, catalog, manual checks and rollback:
https://github.com/Krrrrrrk/chained-beasts-mod-foundation/blob/main/docs/INSTALLATION.md
"@ | Set-Content (Join-Path $stage 'README_INSTALL.md') -Encoding utf8
    $files=@(Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object { @{path=([IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/'));sha256=(Get-Sha $_.FullName)} })
    @{schemaVersion=1;version=$script:ReleaseVersion;package=$package.name;runtimeVerified=$false;published=$false;
        gameAssemblySha256=$script:ExpectedGameHash;files=$files;dependency='TermsAndContusions: local.chainedbeasts.modfoundation >=1.0.0 <2.0.0';products=@($products | Where-Object {$package.members -contains $_.name} | Select-Object name,version,sha256)} |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $stage 'package-manifest.json') -Encoding utf8
    $archive=Join-Path $OutputRoot ($package.name+'-'+$script:ReleaseVersion+'.zip')
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
    $result+=@{path=$archive;sha256=(Get-Sha $archive);products=$package.members}
    # Preserve staging for review; all package artifacts are ignored, no recursive cleanup needed.
}
$result | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutputRoot 'packages.json') -Encoding utf8
$result | ForEach-Object { Write-Output ($_.path+' SHA256 '+$_.sha256) }
