# Gives every mod help page a clickable Workshop link in its Versions row.
# Bare "Workshop <id>" tokens become links; pages whose row carries only a
# mod version (or nothing) get ", <Name> [Workshop <id>](url)" appended,
# plus ", files <date>" when the mod is in the local Workshop folder.
# Idempotent: rows already linking an id are left alone.
#
#   pwsh link-workshop-ids.ps1 -Map <mapping.psd1>
# The mapping file is a hashtable: '<help-english-chapter-dir>' = @{
#   '<slug>' = @( @{ Name = '<mod name as the row spells it>'; Id = <id> }, ... ) }
param([Parameter(Mandatory)][string]$Map)
$workshop = 'C:/Program Files (x86)/Steam/steamapps/workshop/content/294100'
$url = 'https://steamcommunity.com/sharedfiles/filedetails/?id='
$mapping = Import-PowerShellDataFile $Map

function FileDate($id) {
    $dir = Join-Path $workshop $id
    if (-not (Test-Path $dir)) { return $null }
    (Get-ChildItem -LiteralPath $dir -Recurse -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime.ToString('yyyy-MM-dd')
}
function LinkText($name, $id) {
    $s = "$name [Workshop $id]($url$id)"
    $d = FileDate $id
    if ($d) { $s += ", files $d" }
    $s
}

foreach ($chapter in $mapping.Keys) {
    foreach ($file in Get-ChildItem $chapter -Filter '*.md') {
        if ($file.Name -notmatch '^\d+-(.+)\.md$') { continue }
        $slug = $matches[1]
        $lines = Get-Content $file.FullName -Encoding UTF8
        $i = [array]::FindIndex($lines, [Predicate[string]] { param($l) $l -like '| **Versions** |*' })
        if ($i -lt 0) { continue }
        $row = $lines[$i]
        # Bare ids first.
        $row = [regex]::Replace($row, '(?<!\[)Workshop (\d+)(?!\])', {
            param($m) $id = $m.Groups[1].Value
            "[Workshop $id]($url$id)"
        })
        foreach ($entry in @($mapping[$chapter][$slug])) {
            if ($null -eq $entry) { continue }
            $id = $entry.Id; $name = $entry.Name
            if ($row -like "*id=$id)*") { continue }
            $seg = [regex]::Match($row, [regex]::Escape($name) + '[^,|]*')
            if ($seg.Success) {
                $end = $seg.Index + $seg.Value.TrimEnd().Length
                $row = $row.Substring(0, $end) + ', ' + (LinkText '' $id).TrimStart() + $row.Substring($end)
            }
            else {
                $row = $row.TrimEnd().TrimEnd('|').TrimEnd() + ', ' + (LinkText $name $id) + ' |'
            }
        }
        if ($row -ne $lines[$i]) {
            $lines[$i] = $row
            [IO.File]::WriteAllLines($file.FullName, $lines, (New-Object System.Text.UTF8Encoding $false))
            "linked: $($file.FullName)"
        }
    }
}
