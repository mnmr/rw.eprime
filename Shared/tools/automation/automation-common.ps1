Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:AutomationRepositoryRoot = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..\..'))
$script:AutomationProfilePath = Join-Path $script:AutomationRepositoryRoot `
    'AutomationProfiles\Shared'
$script:RimWorldExecutable = `
    'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe'
$script:RimWorldPlayerLog = Join-Path ([Environment]::GetFolderPath('UserProfile')) `
    'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log'

Add-Type -AssemblyName System.Drawing
if ($null -eq ('RimWorldSharedAutomation.Win32' -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace RimWorldSharedAutomation {
  public static class Win32 {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint processId);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  }
}
"@
}
[RimWorldSharedAutomation.Win32]::SetProcessDPIAware() | Out-Null

function Test-SharedProfileCommandLine {
    param([AllowNull()][string]$CommandLine)

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $false
    }
    $escapedPath = [regex]::Escape($script:AutomationProfilePath)
    $pattern = '(?i)(?:^|\s)-savedatafolder=(?:"' + $escapedPath +
        '"|' + $escapedPath + ')(?=\s|$)'
    return [regex]::IsMatch($CommandLine, $pattern)
}

function Get-AllRimWorldProcessInfo {
    return @(Get-CimInstance Win32_Process -Filter "Name = 'RimWorldWin64.exe'")
}

function Get-SharedRimWorldProcessInfo {
    return @(Get-AllRimWorldProcessInfo | Where-Object {
        Test-SharedProfileCommandLine $_.CommandLine
    })
}

function Assert-NoSharedRimWorldProcess {
    $matches = @(Get-SharedRimWorldProcessInfo)
    if ($matches.Count -ne 0) {
        throw "shared automation profile is in use by $($matches.Count) RimWorld process(es)"
    }
}

function Get-ExactlyOneSharedRimWorldProcessInfo {
    $matches = @(Get-SharedRimWorldProcessInfo)
    if ($matches.Count -ne 1) {
        throw "expected one RimWorld process for $script:AutomationProfilePath; found $($matches.Count)"
    }
    return $matches[0]
}

function Get-DesktopState {
    $cursor = New-Object RimWorldSharedAutomation.Win32+POINT
    [RimWorldSharedAutomation.Win32]::GetCursorPos([ref]$cursor) | Out-Null
    return [pscustomobject]@{
        Foreground = [RimWorldSharedAutomation.Win32]::GetForegroundWindow()
        CursorX = $cursor.X
        CursorY = $cursor.Y
    }
}

function Restore-DesktopState {
    param([Parameter(Mandatory)]$State)

    if ($State.Foreground -ne [IntPtr]::Zero) {
        [RimWorldSharedAutomation.Win32]::SetForegroundWindow(
            $State.Foreground) | Out-Null
    }
    [RimWorldSharedAutomation.Win32]::SetCursorPos(
        $State.CursorX, $State.CursorY) | Out-Null
}

function Get-SharedWindowGeometry {
    param([Parameter(Mandatory)][IntPtr]$Handle)

    $client = New-Object RimWorldSharedAutomation.Win32+RECT
    if (-not [RimWorldSharedAutomation.Win32]::GetClientRect(
            $Handle, [ref]$client)) {
        throw 'could not read the shared game client rectangle'
    }
    $origin = New-Object RimWorldSharedAutomation.Win32+POINT
    if (-not [RimWorldSharedAutomation.Win32]::ClientToScreen(
            $Handle, [ref]$origin)) {
        throw 'could not resolve the shared game client origin'
    }
    $width = $client.Right - $client.Left
    $height = $client.Bottom - $client.Top
    if ($width -le 0 -or $height -le 0) {
        throw "invalid shared game client rectangle ${width}x${height}"
    }
    return [pscustomobject]@{
        X = $origin.X
        Y = $origin.Y
        Width = $width
        Height = $height
    }
}

function Invoke-WithSharedGameWindow {
    param([Parameter(Mandatory)][scriptblock]$Action)

    $match = Get-ExactlyOneSharedRimWorldProcessInfo
    $process = Get-Process -Id $match.ProcessId
    $handle = $process.MainWindowHandle
    if ($handle -eq [IntPtr]::Zero) {
        throw 'shared RimWorld process has no main window'
    }

    $desktopState = Get-DesktopState
    $shell = $null
    try {
        $shell = New-Object -ComObject WScript.Shell
        $focused = $false
        for ($attempt = 0; $attempt -lt 10 -and -not $focused; $attempt++) {
            [RimWorldSharedAutomation.Win32]::ShowWindow($handle, 9) | Out-Null
            $shell.AppActivate($process.Id) | Out-Null
            $foregroundHandle = [RimWorldSharedAutomation.Win32]::GetForegroundWindow()
            [uint32]$foregroundPid = 0
            [uint32]$foregroundThread = `
                [RimWorldSharedAutomation.Win32]::GetWindowThreadProcessId(
                    $foregroundHandle, [ref]$foregroundPid)
            [uint32]$targetPid = 0
            [uint32]$targetThread = `
                [RimWorldSharedAutomation.Win32]::GetWindowThreadProcessId(
                    $handle, [ref]$targetPid)
            [uint32]$currentThread = `
                [RimWorldSharedAutomation.Win32]::GetCurrentThreadId()
            [RimWorldSharedAutomation.Win32]::AttachThreadInput(
                $currentThread, $foregroundThread, $true) | Out-Null
            [RimWorldSharedAutomation.Win32]::AttachThreadInput(
                $currentThread, $targetThread, $true) | Out-Null
            try {
                [RimWorldSharedAutomation.Win32]::BringWindowToTop($handle) | Out-Null
                [RimWorldSharedAutomation.Win32]::SetActiveWindow($handle) | Out-Null
                [RimWorldSharedAutomation.Win32]::SetFocus($handle) | Out-Null
                [RimWorldSharedAutomation.Win32]::SetForegroundWindow($handle) | Out-Null
            }
            finally {
                [RimWorldSharedAutomation.Win32]::AttachThreadInput(
                    $currentThread, $targetThread, $false) | Out-Null
                [RimWorldSharedAutomation.Win32]::AttachThreadInput(
                    $currentThread, $foregroundThread, $false) | Out-Null
            }
            Start-Sleep -Milliseconds 50
            $foregroundHandle = [RimWorldSharedAutomation.Win32]::GetForegroundWindow()
            [uint32]$foregroundPid = 0
            [RimWorldSharedAutomation.Win32]::GetWindowThreadProcessId(
                $foregroundHandle, [ref]$foregroundPid) | Out-Null
            $focused = $foregroundPid -eq $process.Id
        }
        if (-not $focused) {
            throw "shared game pid $($process.Id) is not foreground (foreground pid $foregroundPid)"
        }
        return & $Action $process $handle
    }
    finally {
        Restore-DesktopState $desktopState
        if ($null -ne $shell) {
            [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
        }
    }
}

function Wait-ForSharedMapPixels {
    param(
        [Parameter(Mandatory)][Diagnostics.Process]$Process,
        [Parameter(Mandatory)][IntPtr]$Handle,
        [datetime]$Deadline = (Get-Date).AddMinutes(12)
    )

    $geometry = Get-SharedWindowGeometry $Handle
    if ($geometry.Width -ne 1920 -or $geometry.Height -ne 1080) {
        throw "shared profile requires a 1920x1080 client; got $($geometry.Width)x$($geometry.Height)"
    }
    $probeBitmap = New-Object System.Drawing.Bitmap(1, 1)
    $probeGraphics = [System.Drawing.Graphics]::FromImage($probeBitmap)
    try {
        do {
            $Process.Refresh()
            if ($Process.HasExited) {
                throw 'shared game exited before rendering the map'
            }
            $matchingPixels = 0
            foreach ($probeX in 10, 100, 500, 1000, 1500) {
                $probeGraphics.CopyFromScreen(
                    $geometry.X + $probeX,
                    $geometry.Y + $geometry.Height - 30,
                    0, 0, $probeBitmap.Size)
                $color = $probeBitmap.GetPixel(0, 0)
                if ($color.R -ge 28 -and $color.R -le 38 -and
                        $color.G -ge 38 -and $color.G -le 48 -and
                        $color.B -ge 44 -and $color.B -le 54) {
                    $matchingPixels++
                }
            }
            if ($matchingPixels -ge 4) {
                return
            }
            Start-Sleep -Seconds 2
        } while ((Get-Date) -lt $Deadline)
    }
    finally {
        $probeGraphics.Dispose()
        $probeBitmap.Dispose()
    }
    throw 'shared game did not render the map UI before the deadline'
}

function Read-TextFileWhileOpen {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [IO.File]::Open(
        $Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object IO.StreamReader($stream)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}
