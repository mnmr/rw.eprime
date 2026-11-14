param([string]$Label = 'hidden')
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tools = Join-Path $source 'Shared\tools\automation'
$run = & "$tools\create-run.ps1" -Purpose 'WorkRoles hidden roles: editor checkbox, palette hiding, + menu still lists the role' -ModSet workroles-hidden-roles -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -AdditionalModSource @("$PSScriptRoot\Probe")
$out = Join-Path $source "WorkRoles\temp\hidden-roles\$Label-$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools\launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -AllowRecoverableSaveErrors
    . "$tools\automation-common.ps1" -RunId $run.runId
    $stageFile = Join-Path $run.profile 'hidden-stage.txt'
    $deadline = [DateTime]::UtcNow.AddSeconds(58)
    function Wait-Stage([string]$Expected) {
        do {
            Invoke-SharedGameCommand @{command='status'} | Out-Null
            $stage = if (Test-Path $stageFile) { (Get-Content $stageFile -Raw).Trim() } else { '' }
            if ($stage.StartsWith('FAILED')) { throw $stage }
            if ($stage -eq $Expected) { return }
            if ([DateTime]::UtcNow -gt $deadline) { throw "Expected stage $Expected; got $stage" }
            Start-Sleep -Milliseconds 100
        } while ($true)
    }
    function Capture([string]$Name, [switch]$KeepCursor) {
        if ($KeepCursor) { & "$tools\capture.ps1" -RunId $run.runId -OutName "$Name.png" -KeepCursor | Out-Null }
        else { & "$tools\capture.ps1" -RunId $run.runId -OutName "$Name.png" | Out-Null }
    }
    function Click-Hidden {
        $point = (Get-Content (Join-Path $run.profile 'hidden-point.txt') -Raw).Trim().Split(',')
        Invoke-SharedGameCommand @{command='click'; x=[int]$point[0]; y=[int]$point[1]} | Out-Null
    }
    Wait-Stage 'palette-before'; Capture 'palette-before'
    'go-editor' | Set-Content $stageFile
    Wait-Stage 'editor-before'; Capture 'editor-before'
    Click-Hidden
    Wait-Stage 'editor-hidden'; Capture 'editor-hidden'
    'go-colonists' | Set-Content $stageFile
    Wait-Stage 'palette-hidden'; Capture 'palette-hidden'
    'go-menu' | Set-Content $stageFile
    Wait-Stage 'menu'; Capture 'menu' -KeepCursor
    'go-unhide' | Set-Content $stageFile
    Wait-Stage 'editor-unhide'
    Click-Hidden
    Wait-Stage 'SUCCESS'; Capture 'editor-unhidden'
} finally {
    try { & "$tools\stop.ps1" -RunId $run.runId } catch { Write-Warning $_ }
    foreach ($name in 'Player.log','Player.log.host-error.txt','run.json','launch.json','host.json','stall.json','hard-cap.json','Captures','hidden-stage.txt','hidden-roles.txt') {
        $path = Join-Path $run.profile $name
        if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination $out -Recurse -Force }
    }
    & "$tools\remove-run.ps1" -RunId $run.runId
}
Write-Output $out
Get-Content (Join-Path $out 'hidden-roles.txt')
if ((Get-Content (Join-Path $out 'hidden-stage.txt') -Raw).Trim() -ne 'SUCCESS') { throw "Scenario did not pass: $out" }
