# Shared Steam desktop session and callback handling for publishing/tag repair.
# Callers validate local input and obtain any confirmation before remote writes.
function Get-WorkshopTags {
    param([xml]$About, [string]$Source)
    $tags = [Collections.Generic.List[string]]::new()
    $translation = $About.SelectSingleNode('/ModMetaData/translationMod')
    $tags.Add($(if ($translation -and [bool]::Parse($translation.InnerText)) { 'Translation' } else { 'Mod' }))
    $versions = $About.SelectNodes('/ModMetaData/supportedVersions/li')
    if ($versions.Count -eq 0) { throw "No supported versions in $Source" }
    foreach ($node in $versions) {
        [version]$version = $null
        if (-not [version]::TryParse($node.InnerText.Trim(), [ref]$version) -or $version.Minor -lt 0) {
            throw "Invalid supported version '$($node.InnerText)' in $Source"
        }
        $tag = '{0}.{1}' -f $version.Major, $version.Minor
        if (-not $tags.Contains($tag)) { $tags.Add($tag) }
    }
    $tags.ToArray()
}

function Assert-WorkshopConnected {
    # Init only connects to the local client; it also succeeds after SteamCMD
    # has replaced that client's server session. Submitting then misleadingly
    # returns FileNotFound when Steam tries to fetch the Workshop item info.
    if (-not [Steamworks.SteamUser]::BLoggedOn()) {
        throw 'Steam desktop client is disconnected from Steam servers. Reconnect or restart Steam and log in as the Workshop item owner, then retry. SteamCMD can replace the desktop login session.'
    }
}

function Invoke-SteamWorkshopProcess {
    param([string]$RimWorldDir, [scriptblock]$Action, [hashtable]$Parameters = @{})
    if ($PSVersionTable.PSVersion -lt [version]'7.6') {
        throw 'Workshop publishing requires PowerShell 7.6 or newer. Use pwsh to run the script.'
    }
    # Shutdown does not unload Steam's overlay. Only this short-lived process
    # loads Steam; confirmations stay in the caller. Pass data as serialized
    # stdin, never command text (descriptions can exceed Windows' command-line
    # limit when encoded). Actions must declare their inputs as parameters.
    $request = @{ Helper = "$PSScriptRoot\steam-workshop.ps1"; GameDirectory = $RimWorldDir; Action = $Action.ToString(); Parameters = $Parameters }
    # Handle the worker's error envelope ourselves, preserving recovery advice
    # even when the calling shell opts into native exit-code exceptions.
    $PSNativeCommandUseErrorActionPreference = $false
    $reply = @($request | & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -NonInteractive -Command {
        Set-StrictMode -Version Latest
        $ErrorActionPreference = 'Stop'
        try {
            $request = @($input)[0]
            . $request.Helper
            $output = @(Invoke-SteamWorkshopSession -RimWorldDir $request.GameDirectory -Action ([scriptblock]::Create($request.Action)) -Parameters $request.Parameters)
            [pscustomobject]@{ Output = $output; Error = $null }
        } catch {
            [pscustomobject]@{ Output = @(); Error = $_.Exception.Message }
            exit 1
        }
    })
    # Native invocation waits for exit. Host messages are forwarded separately;
    # the final envelope keeps them out of returned item IDs/tag result objects.
    if ($reply.Count -eq 0 -or -not $reply[-1].PSObject.Properties['Output']) {
        throw "Steam publishing process exited without a result (code $LASTEXITCODE). Check the Workshop before retrying."
    }
    $response = $reply[-1]
    if ($LASTEXITCODE -ne 0) {
        if ($response.Error) { throw $response.Error }
        throw "Steam publishing process exited with code $LASTEXITCODE. Check the Workshop before retrying."
    }
    $response.Output
}

# Called inside the worker only; owns the native binding and Steam lifecycle.
function Invoke-SteamWorkshopSession {
    param([string]$RimWorldDir, [scriptblock]$Action, [hashtable]$Parameters = @{})
    $binding = Join-Path $RimWorldDir 'com.rlabrecque.steamworks.net.dll'
    $nativePath = Join-Path $RimWorldDir 'steam_api64.dll'
    $nativeHandle = [Runtime.InteropServices.NativeLibrary]::Load((Resolve-Path -LiteralPath $nativePath).Path)
    $oldAppId = $env:SteamAppId
    $oldGameId = $env:SteamGameId
    $initialized = $false
    try {
        $env:SteamAppId = '294100'
        $env:SteamGameId = '294100'
        [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $binding).Path) | Out-Null
        $initialized = [Steamworks.SteamAPI]::Init()
        if (-not $initialized) { throw 'Steam initialization failed. Start Steam and log in as the Workshop item owner.' }
        if ([Steamworks.SteamUtils]::GetAppID().m_AppId -ne 294100) { throw 'Steam initialized for the wrong app; nothing was submitted.' }
        Assert-WorkshopConnected
        & $Action @Parameters
    } finally {
        try {
            if ($initialized) { [Steamworks.SteamAPI]::Shutdown() }
        } finally {
            $env:SteamAppId = $oldAppId
            $env:SteamGameId = $oldGameId
            [Runtime.InteropServices.NativeLibrary]::Free($nativeHandle)
        }
    }
}

function Wait-WorkshopResult {
    param($Call, [type]$ResultType, [string]$Operation, [int]$TimeoutSeconds = 120)
    if ($Call.m_SteamAPICall -eq 0) { throw "Steam could not start $Operation" }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $failed = $false
    while (-not [Steamworks.SteamUtils]::IsAPICallCompleted($Call, [ref]$failed)) {
        if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "Timed out waiting for $Operation. Check the live Workshop item before retrying; the submission may still finish."
        }
        Assert-WorkshopConnected
        # Poll directly without RunCallbacks: Steamworks.NET's dispatcher reads
        # and discards unregistered call results, racing our GetAPICallResult.
        # Steam's asynchronous operation progresses independently of dispatch.
        Start-Sleep -Milliseconds 100
    }
    if ($failed) { throw "Steam transport failed during $Operation. Check the live Workshop item before retrying." }
    $size = [Runtime.InteropServices.Marshal]::SizeOf([type]$ResultType)
    $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
    try {
        $received = [Steamworks.SteamUtils]::GetAPICallResult($Call, $buffer, $size, $ResultType::k_iCallback, [ref]$failed)
        if (-not $received -or $failed) { throw "Could not read Steam's result for $Operation. Check the live Workshop item before retrying." }
        $result = [Runtime.InteropServices.Marshal]::PtrToStructure($buffer, [type]$ResultType)
        if ($result.m_eResult -ne [Steamworks.EResult]::k_EResultOK) {
            Assert-WorkshopConnected
            throw "Steam $Operation failed: $($result.m_eResult)"
        }
        # CreateItem's assigned ID must be persisted before the caller reports
        # an agreement requirement; otherwise a retry creates a duplicate item.
        $result
    } finally {
        [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer)
    }
}
