param([string]$Label='arbiter', [ValidateSet('host','client')][string]$Role='host', [string]$Out='',
 [string]$ModSet='workroles-mp-clean', [int]$Port=30502,
 # The clean fixture from prepare-fixture.ps1 (vanilla, DLCs, Harmony, Prepatcher, our mods).
 [string]$FixtureDir='')
if(-not $FixtureDir){$FixtureDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\temp\mp-desync\clean-fixture'))}
# Real multiplayer check on the local Windows 1.6 install. The host run hosts
# with the Multiplayer Arbiter (a second headless game joining over 127.0.0.1);
# once its server is up a client run (a second player) joins over loopback.
# Host and client both exercise the WorkRoles UI commands; Multiplayer reports
# any divergence between the simulations as a desync with stack traces.
# Fixed wall-clock budget: 5 minutes after game-ready per run (owner, for MP).
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools=Join-Path $source 'Shared\tools\automation'
$params=@{Purpose="WorkRoles multiplayer desync check $Role ($Label)";ModSourceRoot=$source;ModSet=$ModSet
 AdditionalModSource=@("$PSScriptRoot\Probe");SourceSaveDirectory=$FixtureDir;SavePattern='Fisso-NAM-clean.rws';BaselineConfigPath=(Join-Path $FixtureDir 'ModsConfig.xml')
 GameVersion='1.6'} # the local copy in GameInstalls\1.6; Multiplayer supports 1.6 only
if(-not $Out){$Out=Join-Path $source "WorkRoles\temp\mp-desync\mp-$Label-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"}
$run=& "$tools\create-run.ps1" @params
$dir=Join-Path $Out "$Role-$($run.runId)"
New-Item -ItemType Directory -Path $dir -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $run.profile 'mp-port.txt'),[string]$Port)
if($Role -eq 'client'){
 [IO.File]::WriteAllText((Join-Path $run.profile 'mp-role.txt'),'client')
 # Started only once the host's server is up: join straight from the main
 # menu like a player, without simulating the loaded save first (a process
 # that ticked a single-player game before joining desyncs, mods or not).
 [IO.File]::WriteAllText((Join-Path $run.profile 'mp-server-up.txt'),'up')
}
$stage='';$client=$null
try {
 & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -HardCapSeconds 300 -AllowRecoverableSaveErrors
 . "$tools\automation-common.ps1" -RunId $run.runId
 $deadline=[DateTime]::UtcNow.AddSeconds(300)
 $file=Join-Path $run.profile 'arbiter-stage.txt'
 do {
  try { Invoke-SharedGameCommand @{command='status'} | Out-Null } catch { Write-Warning $_; break }
  $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
  if($Role -eq 'host' -and -not $client -and $stage -like 'RUNNING*' -and $stage -ne 'RUNNING hosting'){
   # The server is up: start the second player.
   $client=Start-Process pwsh -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Out 'client-runner.txt') `
    -ArgumentList @('-NoProfile','-File',$PSCommandPath,'-Label',$Label,'-Role','client','-Out',$Out,'-ModSet',$ModSet,'-Port',$Port,'-FixtureDir',$FixtureDir)
  }
  if($stage.StartsWith('FAILED') -or $stage -eq 'SUCCESS' -or $stage -eq 'DESYNC'){break}
  if([DateTime]::UtcNow -gt $deadline){Write-Warning "Budget exhausted: $stage";break}
  Start-Sleep -Milliseconds 500
 }while($true)
 $stage=if(Test-Path $file){(Get-Content $file -Raw).Trim()}else{''}
} finally {
 for($attempt=0; $attempt -lt 20; $attempt++) {
  try { & "$tools\stop.ps1" -RunId $run.runId; break } catch { Start-Sleep -Milliseconds 500 }
 }
 foreach($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','stall.json','hard-cap.json','Captures','arbiter-stage.txt','arbiter-status.txt','world-rand.txt','MpDesyncs') {
  $path=Join-Path $run.profile $name
  if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $dir -Recurse -Force}
 }
 Get-ChildItem -LiteralPath $run.profile -Recurse -Filter 'arbiter_log.txt' -ErrorAction SilentlyContinue | Copy-Item -Destination $dir -Force
 & "$tools\remove-run.ps1" -RunId $run.runId
}
if($client){ if(-not $client.WaitForExit(120000)){Write-Warning 'Client runner still running'} }
foreach($status in Get-ChildItem -LiteralPath $Out -Recurse -Filter 'arbiter-status.txt'){ "== $($status.Directory.Name)"; Get-Content $status.FullName }
Write-Output "RESULT $Role $stage $Out"
