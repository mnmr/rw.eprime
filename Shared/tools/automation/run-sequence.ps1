[CmdletBinding()]
param([Parameter(Mandatory)][string]$File, [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
# Coordinates are physical pixels within the 1920x1080 game frame. Commands
# affect only the isolated game. No foreground session or desktop input exists.
if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { throw "Action file not found: $File" }
$actions = @(Get-Content -LiteralPath $File | ForEach-Object {
    $line = $_.Trim()
    # Whole-line comments and trailing comments are supported; literal text keeps #.
    if ($line -notmatch '^type\s') { $line = (($line -split '#', 2)[0].Trim() -replace '\s+', ' ') }
    if ($line) { $line }
})
# Validate the whole action file before dispatch; omitted coordinates must never
# silently become (0,0), and an invalid delay must not execute its preceding click.
foreach ($action in $actions) {
    $valid = switch -Regex ($action) {
        '^(click|rclick|hover) \d+ \d+( \d+)?$' { $true; break }
        '^drag \d+ \d+ \d+ \d+( \d+)?$' { $true; break }
        '^scroll \d+ \d+ -?\d+$' { $true; break }
        '^type\s+.+$' { $true; break }
        '^capture [^\\/:*?"<>|\s]+( cursor)?$' { $true; break }
        '^sleep \d+$' { $true; break }
        default { $false }
    }
    if (-not $valid) { throw "Malformed action: $action" }
    $parts = -split $action
    if ($parts[0] -notin 'capture', 'type') {
        for ($i = 1; $i -lt $parts.Count; $i++) {
            $number = 0
            if (-not [int]::TryParse($parts[$i], [ref]$number)) { throw "Invalid integer in action: $action" }
        }
    }
    if ($parts[0] -eq 'scroll' -and [Math]::Abs([long]$parts[3]) -gt 100) { throw 'Scroll is limited to 100 notches.' }
    if ($parts[0] -eq 'capture') {
        $extension = [IO.Path]::GetExtension($parts[1])
        if ($extension -and $extension -ine '.png') { throw 'Capture name must use the PNG extension.' }
    }
    $delay = switch ($parts[0]) {
        { $_ -in 'click', 'rclick', 'hover' } { if ($parts.Count -eq 4) { [long]$parts[3] } }
        'drag' { if ($parts.Count -eq 6) { [long]$parts[5] } }
        'sleep' { [long]$parts[1] }
    }
    if ($null -ne $delay -and $delay -gt 60000) { throw 'Action wait must be between 0 and 60000 milliseconds.' }
}
foreach ($action in $actions) {
    $parts = -split $action
    $wait = 0
    switch ($parts[0]) {
        { $_ -in 'click', 'rclick', 'hover' } {
            $command = if ($parts[0] -eq 'hover') { 'hover' } else { 'click' }
            Invoke-SharedGameCommand @{command=$command; x=[int]$parts[1]; y=[int]$parts[2]; button=[int]($parts[0] -eq 'rclick')} | Out-Null
            $wait = if ($parts.Count -ge 4) { [int]$parts[3] } elseif ($command -eq 'hover') { 900 } else { 500 }
        }
        'drag' {
            Invoke-SharedGameCommand @{command='drag'; x=[int]$parts[1]; y=[int]$parts[2]; x2=[int]$parts[3]; y2=[int]$parts[4]} | Out-Null
            $wait = if ($parts.Count -ge 6) { [int]$parts[5] } else { 500 }
        }
        'scroll' {
            Invoke-SharedGameCommand @{command='scroll'; x=[int]$parts[1]; y=[int]$parts[2]; notches=[int]$parts[3]} | Out-Null
            $wait = 300
        }
        'type' {
            Invoke-SharedGameCommand @{command='type'; text=($action -split '\s+', 2)[1]} | Out-Null
            $wait = 400
        }
        'capture' { Save-SharedGameCapture $parts[1] -Cursor:($parts.Count -ge 3 -and $parts[2] -eq 'cursor') | Out-Null }
        'sleep' { $wait = [int]$parts[1] }
        default { throw "Unknown action: $action" }
    }
    if ($wait -lt 0 -or $wait -gt 60000) { throw 'Action wait must be between 0 and 60000 milliseconds.' }
    if ($wait -gt 0) { Start-Sleep -Milliseconds $wait }
}
Write-Host "sequence complete: $($actions.Count) actions"
