# Calls one RimBridgeServer tool in the selected run and prints its JSON result.
#   bridge.ps1 -RunId <id> -Tool rimworld/list_colonists
#   bridge.ps1 -RunId <id> -Tool rimworld/get_ui_layout -Arguments @{ surfaceId = 'selection-gizmos' }
# Rects in results are logical UI pixels: multiply by the run's uiScale (1.25)
# for pipe input coordinates. Scripts making many calls can dot-source
# bridge-common.ps1 and reuse one Connect-RunBridge session.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Tool, [hashtable]$Arguments = @{}, [string]$OutFile = '', [string]$RunId = $env:RIMWORLD_AUTOMATION_RUN_ID)
. (Join-Path $PSScriptRoot 'automation-common.ps1') -RunId $RunId
. (Join-Path $PSScriptRoot 'bridge-common.ps1')
$session = Connect-RunBridge
try { $result = Invoke-RunBridgeTool $session $Tool $Arguments }
finally { $session.Client.Dispose() }
$json = $result | ConvertTo-Json -Depth 100
if ($OutFile) { Set-Content -LiteralPath $OutFile -Value $json -Encoding utf8 } else { $json }
