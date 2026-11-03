param(
    [Parameter(Mandatory)][string]$Save,
    [switch]$WhatIf
)
# Repairs pawns whose saved current job has no <def>: a job whose JobDef was
# already gone when the game saved. RimWorld's own load cleanup throws on such
# a job (Pawn_JobTracker.EndCurrentJob needs the def), so the pawn keeps a
# defless job and errors on PostMapInit pathing and on every tick. The job is
# replaced by a null job, the pawn's pather is told it is not moving (the
# state a pawn is in right after a job ends), and the map reservations that
# job held are dropped (the game would delete them itself with one error per
# reservation on the next load).
# A backup is written beside the save as <name>.rws.<timestamp>.bak (not a
# .rws, so run creation never picks it up).
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Save)) { throw "Save not found: $Save" }
$text = [IO.File]::ReadAllText($Save)
$jobs = [regex]::Matches($text, '<curJob>(.*?)</curJob>', 'Singleline')
$repairs = @()
foreach ($m in $jobs) {
    $body = $m.Groups[1].Value
    if ($body -match '<def>') { continue }
    $idAt = $text.LastIndexOf('<id>', $m.Index)
    $idEnd = $text.IndexOf('</id>', $idAt)
    $loadId = if ($body -match '<loadID>(\d+)</loadID>') { $Matches[1] } else { $null }
    $repairs += [pscustomobject]@{ Id = $text.Substring($idAt + 4, $idEnd - $idAt - 4); Index = $m.Index; Length = $m.Length; LoadId = $loadId }
}
if ($repairs.Count -eq 0) { Write-Output "No defless current jobs in $Save"; return }
foreach ($r in $repairs) { Write-Output "defless current job: $($r.Id) (job load id $($r.LoadId))" }
if ($WhatIf) { return }

# Apply from the end so earlier indexes stay valid.
foreach ($r in ($repairs | Sort-Object Index -Descending)) {
    $nextThing = $text.IndexOf('<thing ', $r.Index)
    if ($nextThing -lt 0) { $nextThing = $text.Length }
    $pather = $text.IndexOf('<pather>', $r.Index)
    if ($pather -ge 0 -and $pather -lt $nextThing) {
        $patherEnd = $text.IndexOf('</pather>', $pather)
        $patherBody = $text.Substring($pather, $patherEnd - $pather)
        if ($patherBody -notmatch '<moving>') {
            $text = $text.Insert($pather + 8, "`n`t`t`t`t`t`t`t<moving>False</moving>")
        } else {
            $text = $text.Substring(0, $pather) + $patherBody.Replace('<moving>True</moving>', '<moving>False</moving>') + $text.Substring($patherEnd)
        }
    }
    $text = $text.Substring(0, $r.Index) + '<curJob IsNull="True" />' + $text.Substring($r.Index + $r.Length)
}

# Reservations held by the removed jobs (ReservationManager entries).
$dropped = 0
foreach ($r in $repairs) {
    if (-not $r.LoadId) { continue }
    $pattern = '\s*<li>\s*<claimant>[^<]*</claimant>\s*<job>Job_' + $r.LoadId + '</job>.*?</li>'
    $dropped += ([regex]::Matches($text, $pattern, 'Singleline')).Count
    $text = [regex]::Replace($text, $pattern, '', 'Singleline')
}
$remaining = 0
foreach ($r in $repairs) { if ($r.LoadId) { $remaining += ([regex]::Matches($text, 'Job_' + $r.LoadId + '\b')).Count } }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "$Save.$stamp.bak"
Copy-Item -LiteralPath $Save -Destination $backup
[IO.File]::WriteAllText($Save, $text, [Text.UTF8Encoding]::new($false))
Write-Output "Repaired $($repairs.Count) pawn(s); dropped $dropped reservation(s); references to their jobs left: $remaining; backup at $backup"
