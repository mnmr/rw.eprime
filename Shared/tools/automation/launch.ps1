[CmdletBinding()]
param([int]$TimeoutMinutes = 12)
. (Join-Path $PSScriptRoot 'automation-common.ps1')
$hostExecutable = Join-Path $script:AutomationRepositoryRoot 'Shared\Automation\src\RimWorld.Automation.Host\bin\Release\net10.0-windows\RimWorld.Automation.Host.exe'
foreach ($path in @($hostExecutable, $script:RimWorldExecutable,
    (Join-Path $script:AutomationProfilePath 'Config\Prefs.xml'),
    (Join-Path $script:AutomationProfilePath 'Config\ModsConfig.xml'),
    (Join-Path $script:AutomationProfilePath 'Saves\Autostart.rws'))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Automation is not ready: $path. Build, deploy, and refresh the shared profile first." }
}
if (@(Get-AllRimWorldProcessInfo).Count -ne 0) { throw 'Refusing to launch while another RimWorld process exists.' }
[xml]$config = Get-Content (Join-Path $script:AutomationProfilePath 'Config\ModsConfig.xml') -Raw
if (@($config.ModsConfigData.activeMods.li) -notcontains 'eprime.sharedautomation') { throw 'Refresh the shared profile to enable its automation runtime.' }
$runToken = 'rimworld-shared-' + [Guid]::NewGuid().ToString('N')
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$arguments = '"' + $script:RimWorldExecutable + '" "' + $script:AutomationProfilePath + '" ' + $runToken + ' "' + $script:RimWorldPlayerLog + '"'
$hostProcess = Start-Process -FilePath $hostExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
    do {
        if ($hostProcess.HasExited) { throw "Isolated game host exited; inspect $script:RimWorldPlayerLog.host-error.txt" }
        $processes = @(Get-SharedRimWorldProcessInfo)
        if ($processes.Count -gt 1) { throw 'More than one shared-profile game exists.' }
        if ($processes.Count -eq 1 -and $processes[0].CommandLine.Contains($runToken)) {
            $content = if (Test-Path $script:RimWorldPlayerLog) { Read-TextFileWhileOpen $script:RimWorldPlayerLog } else { '' }
            if ($content.Contains($runToken) -and $content -match '(?m)^SaveableFromNode exception:|^Exception while loading|SharedAutomation.*Exception|^Crash!!!|Could not execute post-long-event action\. Exception:') {
                throw 'The isolated game logged an automation or save-load failure; inspect its Player.log.'
            }
            if ($content.Contains($runToken) -and $content.Contains('[SharedAutomation] ready for background commands')) {
                $state = Invoke-SharedGameCommand @{ command = 'status' }
                if ($state.ready) {
                    if ($state.width -ne 1920 -or $state.height -ne 1080) { throw "Unexpected game frame: $($state.width)x$($state.height)" }
                    Save-SharedGameCapture 'launch-ready.png' | Out-Null
                    Write-Host "shared game ready in background pid=$($state.processId) profile=$script:AutomationProfilePath"
                    exit 0
                }
            }
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw 'Shared game did not become ready before the deadline.'
} catch {
    $processes = @(Get-SharedRimWorldProcessInfo)
    if ($processes.Count -eq 1 -and $processes[0].CommandLine.Contains($runToken)) { Stop-Process -Id $processes[0].ProcessId -Force }
    if (-not $hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id -Force }
    throw
}
