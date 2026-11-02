# Release helpers for publish-mod.ps1: the changelog section for the version
# being published, its Workshop change note (BBCode), and each mod's record of
# released versions (RELEASES.md, written only after a successful upload).

$script:ChangelogCategories = @('Added', 'Changed', 'Fixed', 'Removed')

# Entries of the "## <version>" section: top-level "- Category: text" items
# (continuation lines are joined) with optional indented "- " sub-items. An
# entry without a category is accepted only as the sole entry (a first
# release, e.g. "- Initial release.").
function Get-ChangelogRelease {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Version)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Changelog not found: $Path" }
    $lines = Get-Content -LiteralPath $Path
    $heading = '^##\s+' + [regex]::Escape($Version) + '(\s|$)'
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match $heading) { $start = $i; break } }
    if ($start -lt 0) { throw "CHANGELOG.md has no '## $Version' section; add the release notes before publishing." }
    $entries = [Collections.Generic.List[object]]::new()
    $current = $null
    for ($i = $start + 1; $i -lt $lines.Count -and $lines[$i] -notmatch '^##\s'; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*$') { continue }
        if ($line -match '^- (.+)$') {
            $text = $Matches[1].Trim()
            $category = ''
            if ($text -match '^(\w+):\s+(.+)$' -and $script:ChangelogCategories -contains $Matches[1]) {
                $category = $Matches[1]; $text = $Matches[2]
            }
            $current = [pscustomobject]@{ Category = $category; Text = $text; Items = [Collections.Generic.List[string]]::new() }
            $entries.Add($current)
        } elseif ($line -match '^\s+- (.+)$' -and $current) {
            $current.Items.Add($Matches[1].Trim())
        } elseif ($current) {
            # Wrapped continuation of the previous entry or sub-item.
            if ($current.Items.Count -gt 0) { $current.Items[$current.Items.Count - 1] += ' ' + $line.Trim() }
            else { $current.Text += ' ' + $line.Trim() }
        } else {
            throw "Unexpected text before the first entry of '## $Version': $line"
        }
    }
    if ($entries.Count -eq 0) { throw "CHANGELOG.md section '## $Version' has no entries." }
    $uncategorized = @($entries | Where-Object { -not $_.Category })
    if ($uncategorized.Count -gt 0 -and $entries.Count -gt 1) {
        throw "Every entry in '## $Version' needs a category ($($script:ChangelogCategories -join '/')): $($uncategorized[0].Text)"
    }
    return ,$entries.ToArray()
}

# Markdown spans used in changelogs, as Steam BBCode.
function ConvertTo-WorkshopBbcodeText([string]$Text) {
    $Text = [regex]::Replace($Text, '\[([^\]]+)\]\(([^)\s]+)\)', '[url=$2]$1[/url]')
    $Text = [regex]::Replace($Text, '\*\*([^*]+)\*\*', '[b]$1[/b]')
    $Text = [regex]::Replace($Text, '`([^`]+)`', '$1')
    return $Text
}

# "v<version>" followed by the entries in Added, Changed, Fixed, Removed order
# (changelog order within a category).
function ConvertTo-WorkshopChangeNote {
    param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][object[]]$Entries)
    $ordered = @($Entries | Sort-Object -Stable { $index = $script:ChangelogCategories.IndexOf($_.Category); if ($index -lt 0) { -1 } else { $index } })
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append("v$Version`n[list]`n")
    foreach ($entry in $ordered) {
        $label = if ($entry.Category) { "$($entry.Category): " } else { '' }
        [void]$builder.Append('[*]').Append($label).Append((ConvertTo-WorkshopBbcodeText $entry.Text)).Append("`n")
        if ($entry.Items.Count -gt 0) {
            [void]$builder.Append("[list]`n")
            foreach ($item in $entry.Items) { [void]$builder.Append('[*]').Append((ConvertTo-WorkshopBbcodeText $item)).Append("`n") }
            [void]$builder.Append("[/list]`n")
        }
    }
    [void]$builder.Append('[/list]')
    return $builder.ToString()
}

# RELEASES.md: one table row per published version (version, UTC time, item).
function Get-ReleaseRecord {
    param([Parameter(Mandatory)][string]$ModRoot, [Parameter(Mandatory)][string]$Version)
    $path = Join-Path $ModRoot 'RELEASES.md'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\|\s*([^|\s]+)\s*\|\s*([^|]+?)\s*\|\s*([^|]*?)\s*\|$' -and $Matches[1] -ceq $Version) {
            return [pscustomobject]@{ Version = $Matches[1]; Published = $Matches[2]; Item = $Matches[3] }
        }
    }
    return $null
}

# The newest change note on the item's public Workshop changelog page, as
# @{ Text; PublishedUtc }, or $null when the page cannot be read. Each note's
# paragraph id is its Unix timestamp.
function Get-WorkshopLatestChangeNote([string]$ItemId) {
    try {
        $html = [string](Invoke-WebRequest -Uri "https://steamcommunity.com/sharedfiles/filedetails/changelog/$ItemId" -UseBasicParsing -TimeoutSec 20).Content
    } catch { return $null }
    $match = [regex]::Match($html, '(?s)class="changelog headline">.*?<p id="(\d+)">(.*?)</p>')
    if (-not $match.Success) { return $null }
    [pscustomobject]@{
        Text = [Net.WebUtility]::HtmlDecode(($match.Groups[2].Value -replace '<br\s*/?>', "`n" -replace '<[^>]+>', ''))
        PublishedUtc = [DateTimeOffset]::FromUnixTimeSeconds([long]$match.Groups[1].Value).UtcDateTime
    }
}

# True when a change note was generated for this version ("v<version>" first line).
function Test-ChangeNoteVersion([string]$Text, [string]$Version) {
    $Text -match ('^\s*v' + [regex]::Escape($Version) + '\s*(\n|$)')
}

# The most recently recorded release (rows are appended in publish order), or
# $null before the first publish.
function Get-LatestReleaseRecord {
    param([Parameter(Mandatory)][string]$ModRoot)
    $path = Join-Path $ModRoot 'RELEASES.md'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    $latest = $null
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\|\s*([^|\s]+)\s*\|\s*([^|]+?)\s*\|\s*([^|]*?)\s*\|$' -and $Matches[1] -ne 'Version' -and $Matches[1] -notmatch '^-+$') {
            $latest = [pscustomobject]@{ Version = $Matches[1]; Published = $Matches[2]; Item = $Matches[3] }
        }
    }
    return $latest
}

# The only version change the publisher accepts unapproved: major.minor.patch
# with the patch exactly one above the previous release. Anything else (a
# skipped patch, a minor or major step, a downgrade, a two- or four-part
# number) needs -ApproveVersion naming the version. Owner, 2026-11-01: a
# 1.2.4 -> 1.2.6 bump shipped a never-published 1.2.5 section unannounced.
function Test-NextPatchVersion([string]$Previous, [string]$Version) {
    $pattern = '^(\d+)\.(\d+)\.(\d+)$'
    if ($Previous -notmatch $pattern) { return $false }
    $previousParts = @([int]$Matches[1], [int]$Matches[2], [int]$Matches[3])
    if ($Version -notmatch $pattern) { return $false }
    return [int]$Matches[1] -eq $previousParts[0] -and [int]$Matches[2] -eq $previousParts[1] `
        -and [int]$Matches[3] -eq $previousParts[2] + 1
}

function Get-NextPatchVersion([string]$Previous) {
    if ($Previous -notmatch '^(\d+)\.(\d+)\.(\d+)$') { return $null }
    return "$($Matches[1]).$($Matches[2]).$([int]$Matches[3] + 1)"
}

function Format-ReleaseRecordLine([string]$Version, [DateTime]$PublishedUtc, [string]$Item) {
    "| $Version | $($PublishedUtc.ToString('yyyy-MM-dd HH:mm')) UTC | $Item |"
}

function Add-ReleaseRecord {
    param([Parameter(Mandatory)][string]$ModRoot, [Parameter(Mandatory)][string]$Line)
    $path = Join-Path $ModRoot 'RELEASES.md'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Set-Content -LiteralPath $path -Encoding utf8 -Value @(
            '# Releases', '', 'Versions published to the Steam Workshop, recorded by the publish script.', '',
            '| Version | Published | Workshop item |', '|---|---|---|')
    }
    Add-Content -LiteralPath $path -Encoding utf8 -Value $Line
}
