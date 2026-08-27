[CmdletBinding()]
param([int]$TimeoutMinutes = 12)

. (Join-Path $PSScriptRoot 'automation-common.ps1')

$required = @(
    (Join-Path $script:AutomationProfilePath 'Config\Prefs.xml'),
    (Join-Path $script:AutomationProfilePath 'Config\ModsConfig.xml'),
    (Join-Path $script:AutomationProfilePath 'Saves\Autostart.rws')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "shared profile is not ready; run refresh-profile.ps1 (missing $path)"
    }
}
if (-not (Test-Path -LiteralPath $script:RimWorldExecutable -PathType Leaf)) {
    throw "RimWorld executable does not exist: $script:RimWorldExecutable"
}

$existing = @(Get-AllRimWorldProcessInfo)
if ($existing.Count -ne 0) {
    throw "refusing to launch while $($existing.Count) RimWorld process(es) exist"
}

$desktopState = Get-DesktopState
$started = Get-Date
$deadline = $started.AddMinutes($TimeoutMinutes)
$runToken = 'rimworld-shared-' + [Guid]::NewGuid().ToString('N')
$process = $null
try {
    $arguments = '-savedatafolder="' + $script:AutomationProfilePath +
        '" -automationtoken=' + $runToken
    $process = Start-Process -FilePath $script:RimWorldExecutable `
        -ArgumentList $arguments -PassThru

    $logReady = $false
    do {
        $process.Refresh()
        if ($process.HasExited) {
            throw 'shared game exited before loading the save'
        }
        $matches = @(Get-SharedRimWorldProcessInfo)
        if ($matches.Count -ne 1 -or
                $matches[0].ProcessId -ne $process.Id) {
            throw "shared process identity mismatch for pid $($process.Id)"
        }
        if (Test-Path -LiteralPath $script:RimWorldPlayerLog -PathType Leaf) {
            $logInfo = Get-Item -LiteralPath $script:RimWorldPlayerLog
            if ($logInfo.LastWriteTime -ge $started) {
                $content = Read-TextFileWhileOpen $script:RimWorldPlayerLog
                $runIndex = $content.IndexOf(
                    $runToken, [StringComparison]::Ordinal)
                $loadIndex = if ($runIndex -lt 0) { -1 } else {
                    $content.IndexOf(
                        'Loading game from file Autostart with mods:',
                        $runIndex, [StringComparison]::Ordinal)
                }
                if ($loadIndex -ge 0) {
                    $afterLoad = $content.Substring($loadIndex)
                    if ($afterLoad -match '(?m)^SaveableFromNode exception:' -or
                            $afterLoad -match '(?m)^Exception while loading') {
                        throw 'shared save logged a load exception; inspect Player.log'
                    }
                    $logReady = $afterLoad -match
                        '(?m)^Unloading \d+ Unused Serialized files'
                }
            }
        }
        if (-not $logReady) {
            Start-Sleep -Seconds 2
        }
    } while (-not $logReady -and (Get-Date) -lt $deadline)
    if (-not $logReady) {
        throw 'shared save did not reach the generic load-complete marker before the deadline'
    }

    Invoke-WithSharedGameWindow {
        param($focusedProcess, $handle)
        Wait-ForSharedMapPixels $focusedProcess $handle $deadline
    }
    Write-Host "shared game ready pid=$($process.Id) token=$runToken"
    Write-Host "profile=$script:AutomationProfilePath"
}
catch {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            $matches = @(Get-SharedRimWorldProcessInfo)
            if ($matches.Count -eq 1 -and
                    $matches[0].ProcessId -eq $process.Id) {
                Stop-Process -Id $process.Id -Force
            }
        }
    }
    throw
}
finally {
    Restore-DesktopState $desktopState
}

