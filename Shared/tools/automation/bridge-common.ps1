# Minimal GABP 1.0 client for RimBridgeServer: LSP-style Content-Length framing
# over loopback TCP. launch.ps1 gives every run its own port and token
# (bridge.json in the run profile); bridge.ps1 is the command entry point.
function Send-GabpFrame($Session, [string]$Json) {
    $body = [Text.Encoding]::UTF8.GetBytes($Json)
    $head = [Text.Encoding]::ASCII.GetBytes("Content-Length: $($body.Length)`r`nContent-Type: application/json`r`n`r`n")
    $Session.Stream.Write($head, 0, $head.Length)
    $Session.Stream.Write($body, 0, $body.Length)
    $Session.Stream.Flush()
}
function Read-GabpFrame($Session) {
    $header = [Text.StringBuilder]::new()
    while (-not $header.ToString().EndsWith("`r`n`r`n")) {
        $b = $Session.Stream.ReadByte()
        if ($b -lt 0) { throw 'GABP connection closed.' }
        if ($header.Length -gt 4096) { throw 'GABP header too long.' }
        [void]$header.Append([char]$b)
    }
    if ($header.ToString() -notmatch '(?im)^Content-Length:\s*(\d{1,9})\s*$') { throw "Bad GABP header: $header" }
    $length = [int]$Matches[1]; $buffer = [byte[]]::new($length); $offset = 0
    while ($offset -lt $length) {
        $read = $Session.Stream.Read($buffer, $offset, $length - $offset)
        if ($read -le 0) { throw 'GABP connection closed mid-frame.' }
        $offset += $read
    }
    [Text.Encoding]::UTF8.GetString($buffer)
}
function Invoke-GabpRequest($Session, [string]$Method, $Params) {
    $id = [Guid]::NewGuid().ToString()
    Send-GabpFrame $Session (@{ v = 'gabp/1'; id = $id; type = 'request'; method = $Method; params = $Params } | ConvertTo-Json -Depth 20 -Compress)
    while ($true) {
        $raw = Read-GabpFrame $Session
        $message = $raw | ConvertFrom-Json -Depth 200
        # Events interleave with responses; only the matching response ends the call.
        if ($message.type -eq 'response' -and $message.id -eq $id) { return [pscustomobject]@{ raw = $raw; message = $message } }
    }
}
function Connect-Gabp([int]$Port, [string]$Token, [int]$TimeoutMilliseconds = 30000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ($true) {
        $client = [Net.Sockets.TcpClient]::new()
        try { $client.Connect([Net.IPAddress]::Loopback, $Port); break }
        catch { $client.Dispose(); if ([DateTime]::UtcNow -gt $deadline) { throw "Bridge did not accept connections on port $Port." }; Start-Sleep -Milliseconds 250 }
    }
    $client.ReceiveTimeout = 120000
    $session = [pscustomobject]@{ Client = $client; Stream = $client.GetStream() }
    $hello = Invoke-GabpRequest $session 'session/hello' @{
        token = $Token; bridgeVersion = '1.0.0'; platform = 'windows'; launchId = [Guid]::NewGuid().ToString()
        clientInfo = @{ name = 'eprime-shared-automation'; version = '1' }
    }
    if ($hello.message.error) { $client.Dispose(); throw "Bridge rejected the session: $($hello.raw)" }
    $session
}
# Opens the selected run's bridge after re-validating its owner and live game.
function Connect-RunBridge {
    $record = Get-SelectedRun
    Get-ExactlyOneSharedRimWorldProcessInfo | Out-Null
    $path = Join-Path $record.profile 'bridge.json'
    if (-not (Test-Path -LiteralPath $path)) { throw 'This run was launched without a bridge endpoint.' }
    $endpoint = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Connect-Gabp ([int]$endpoint.port) ([string]$endpoint.token)
}
function Invoke-RunBridgeTool($Session, [string]$Name, [hashtable]$Arguments = @{}) {
    $reply = Invoke-GabpRequest $Session 'tools/call' @{ name = $Name; arguments = $Arguments }
    Update-RunActivity "bridge $Name" # run-guard counts bridge calls as the script driving the run
    if ($reply.message.error) { throw "Bridge tool $Name failed: $($reply.message.error | ConvertTo-Json -Depth 10 -Compress)" }
    $result = $reply.message.result
    if ($result -and $result.PSObject.Properties['success'] -and -not $result.success) { throw "Bridge tool $Name reported failure: $($reply.raw)" }
    $result
}
