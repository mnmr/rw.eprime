$ErrorActionPreference = 'Stop'
$script:AutomationRepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$script:AutomationProfilePath = Join-Path $script:AutomationRepositoryRoot 'AutomationProfiles\Shared'
$script:RimWorldExecutable = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe'
$script:RimWorldPlayerLog = Join-Path $script:AutomationProfilePath 'Player.log'

function Test-SharedProfileCommandLine {
    param([string]$CommandLine)
    $path = [regex]::Escape($script:AutomationProfilePath)
    return $CommandLine -match ('(?i)(?:^|\s)-savedatafolder=(?:"' + $path + '"|' + $path + ')(?=\s|$)')
}
function Get-AllRimWorldProcessInfo { @(Get-CimInstance Win32_Process -Filter "name = 'RimWorldWin64.exe'") }
function Get-SharedRimWorldProcessInfo { @(Get-AllRimWorldProcessInfo | Where-Object { Test-SharedProfileCommandLine $_.CommandLine }) }
function Assert-NoSharedRimWorldProcess {
    if (@(Get-SharedRimWorldProcessInfo).Count -ne 0) { throw 'Stop the shared-profile game before changing its runtime or profile.' }
}
function Get-ExactlyOneSharedRimWorldProcessInfo {
    $matches = @(Get-SharedRimWorldProcessInfo)
    if ($matches.Count -ne 1) { throw "Expected exactly one shared-profile game; found $($matches.Count)." }
    return $matches[0]
}
function Read-TextFileWhileOpen {
    param([string]$Path)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    $reader = [IO.StreamReader]::new($stream)
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}
function Read-AutomationBytes {
    param([IO.Stream]$Stream, [int]$Count, [Threading.CancellationToken]$CancellationToken)
    $buffer = [byte[]]::new($Count)
    $offset = 0
    while ($offset -lt $Count) {
        $read = $Stream.ReadAsync($buffer, $offset, $Count - $offset, $CancellationToken).GetAwaiter().GetResult()
        if ($read -eq 0) { throw 'Automation connection closed before its reply completed.' }
        $offset += $read
    }
    return ,$buffer
}
function Invoke-SharedGameCommand {
    param([Parameter(Mandatory)][hashtable]$Command, [int]$ConnectTimeoutMilliseconds = 5000)
    # Exact process and fresh launch token on every request. No stale endpoint files.
    $process = Get-ExactlyOneSharedRimWorldProcessInfo
    if ($process.CommandLine -notmatch '(?:^|\s)-automationtoken=(rimworld-shared-[a-f0-9]{32})(?=\s|$)') {
        throw 'Shared game has no background session token; restart with launch.ps1.'
    }
    $pipeName = "rimworld-automation-$($process.ProcessId)-$($Matches[1])"
    $connection = [IO.Pipes.NamedPipeClientStream]::new('.', $pipeName, [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
    $deadline = [Threading.CancellationTokenSource]::new(35000)
    try {
        $connection.Connect($ConnectTimeoutMilliseconds)
        $data = [Text.Encoding]::UTF8.GetBytes(($Command | ConvertTo-Json -Compress))
        if ($data.Length -gt 65536) { throw 'Automation request exceeds 64 KiB.' }
        $header = [BitConverter]::GetBytes([int]$data.Length)
        $connection.WriteAsync($header, 0, 4, $deadline.Token).GetAwaiter().GetResult()
        $connection.WriteAsync($data, 0, $data.Length, $deadline.Token).GetAwaiter().GetResult()
        $connection.Flush()
        # Both peers have deadlines, including a stalled or suspended game process.
        # There is deliberately no desktop-input fallback.
        $header = Read-AutomationBytes $connection 4 $deadline.Token
        $size = [BitConverter]::ToInt32($header, 0)
        if ($size -lt 2 -or $size -gt 67108864) { throw 'Invalid automation reply size.' }
        $reply = [Text.Encoding]::UTF8.GetString((Read-AutomationBytes $connection $size $deadline.Token)) | ConvertFrom-Json
        if (-not $reply.ok) { throw "Game automation failed: $($reply.error)" }
        if ($reply.processId -ne $process.ProcessId) { throw 'Automation reply process identity mismatch.' }
        return $reply
    } finally { $connection.Dispose(); $deadline.Dispose() }
}
function Save-SharedGameCapture {
    param([Parameter(Mandatory)][string]$OutName, [switch]$Cursor)
    if ([IO.Path]::GetExtension($OutName) -eq '') { $OutName += '.png' }
    if ([IO.Path]::GetFileName($OutName) -cne $OutName -or [IO.Path]::GetExtension($OutName) -ine '.png') {
        throw 'Capture name must be a PNG filename without a directory.'
    }
    $reply = Invoke-SharedGameCommand @{ command = 'capture' }
    $directory = Join-Path $script:AutomationProfilePath 'Captures'
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $destination = Join-Path $directory $OutName
    [IO.File]::WriteAllBytes($destination, [Convert]::FromBase64String($reply.png))
    if ($Cursor) {
        # Fixed documentation arrow at the virtual pointer; never the hardware cursor.
        Add-Type -AssemblyName System.Drawing
        $bitmap = [Drawing.Bitmap]::new($destination)
        $copy = [Drawing.Bitmap]::new($bitmap)
        $bitmap.Dispose()
        $graphics = [Drawing.Graphics]::FromImage($copy)
        try {
            $points = [Drawing.Point[]]@(
                [Drawing.Point]::new($reply.x, $reply.y), [Drawing.Point]::new($reply.x, $reply.y + 20),
                [Drawing.Point]::new($reply.x + 5, $reply.y + 15), [Drawing.Point]::new($reply.x + 10, $reply.y + 24),
                [Drawing.Point]::new($reply.x + 14, $reply.y + 22), [Drawing.Point]::new($reply.x + 9, $reply.y + 13),
                [Drawing.Point]::new($reply.x + 16, $reply.y + 13))
            $graphics.FillPolygon([Drawing.Brushes]::White, $points)
            $graphics.DrawPolygon([Drawing.Pens]::Black, $points)
            $copy.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $copy.Dispose() }
    }
    Write-Host "captured $destination"
    return $destination
}
