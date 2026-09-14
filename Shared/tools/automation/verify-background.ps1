[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$File,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID
)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { throw "Action file not found: $File" }
Assert-NoSharedRimWorldProcess
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
. (Join-Path $PSScriptRoot 'foreground-audit.ps1')
[SharedForegroundAudit]::Start()
$testProcessId = $null
$launched = $false
try {
    & (Join-Path $PSScriptRoot 'launch.ps1') -RunId $RunId
    $launched = $true
    $testProcessId = (Invoke-SharedGameCommand @{ command = 'status' }).processId
    & (Join-Path $PSScriptRoot 'run-sequence.ps1') -File $File -RunId $RunId
} finally {
    # Keep the audit running through shutdown, too.
    try {
        if ($launched) { & (Join-Path $PSScriptRoot 'stop.ps1') -RunId $RunId }
    }
    finally {
        $seen = [SharedForegroundAudit]::Finish()
        [ordered]@{ gameProcessId = $testProcessId; foregroundProcessIds = $seen } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'foreground.json')
        if (Test-Path -LiteralPath $script:RimWorldPlayerLog) {
            Copy-Item -LiteralPath $script:RimWorldPlayerLog -Destination (Join-Path $EvidenceDirectory 'Player.log') -Force
        }
    }
}
if (-not $testProcessId) { throw 'No ready test process was observed.' }
if ($seen -contains [uint32]$testProcessId) { throw 'The game acquired foreground focus.' }
Write-Host "Background run passed; foreground process IDs: $($seen -join ', '); game PID: $testProcessId"
