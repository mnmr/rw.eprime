[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$File,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)
. (Join-Path $PSScriptRoot 'automation-common.ps1')
if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { throw "Action file not found: $File" }
Assert-NoSharedRimWorldProcess
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
public static class SharedForegroundAudit {
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    static readonly HashSet<uint> seen = new HashSet<uint>();
    static volatile bool stop;
    static Thread thread;
    public static void Start() {
        seen.Clear(); stop = false;
        thread = new Thread(() => {
            while (!stop) {
                GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);
                lock (seen) seen.Add(processId);
                Thread.Sleep(10);
            }
        });
        thread.IsBackground = true; thread.Start();
    }
    public static uint[] Finish() {
        stop = true; thread.Join();
        lock (seen) { var result = new uint[seen.Count]; seen.CopyTo(result); return result; }
    }
}
'@
[SharedForegroundAudit]::Start()
$testProcessId = $null
try {
    & (Join-Path $PSScriptRoot 'launch.ps1')
    $testProcessId = (Invoke-SharedGameCommand @{ command = 'status' }).processId
    & (Join-Path $PSScriptRoot 'run-sequence.ps1') -File $File
} finally {
    # Keep the audit running through shutdown, too.
    try {
        if ($testProcessId) {
            $running = @(Get-SharedRimWorldProcessInfo)
            if ($running.Count -eq 1 -and $running[0].ProcessId -eq $testProcessId) {
                & (Join-Path $PSScriptRoot 'stop.ps1')
            }
        }
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
