# Renumbers one help chapter A-Z by English title (brackets ignored,
# case-insensitive ordinal) from a start number, mirroring the new numbers
# into every language folder so translated pages keep their slots.
# Pages numbered below -Start (overview, intro pages) are left alone.
#
#   pwsh renumber-help-chapter.ps1 -Help <mod>\mod\Help -Chapter 5-mods -Start 4
param(
    [Parameter(Mandatory)][string]$Help,
    [Parameter(Mandatory)][string]$Chapter,
    [Parameter(Mandatory)][int]$Start
)
$english = Join-Path $Help "English\$Chapter"
$titles = @{}
foreach ($f in Get-ChildItem $english -Filter '*.md') {
    if ($f.Name -notmatch '^(\d+)-(.+)\.md$') { continue }
    if ([int]$matches[1] -lt $Start) { continue }
    $slug = $matches[2]
    $title = (Get-Content $f.FullName -Encoding UTF8 | Where-Object { $_ -like 'title:*' } | Select-Object -First 1) -replace '^title:\s*', ''
    $titles[$slug] = $title
}
$ordered = [System.Linq.Enumerable]::ToArray([System.Linq.Enumerable]::OrderBy(
    [string[]]$titles.Keys, [Func[string, string]] { param($k) ($titles[$k] -replace '[\[\]]', '') },
    [StringComparer]::OrdinalIgnoreCase))
$number = @{}
$n = $Start
foreach ($slug in $ordered) { $number[$slug] = ('{0:00}' -f $n); $n++ }

foreach ($lang in Get-ChildItem $Help -Directory) {
    $dir = Join-Path $lang.FullName $Chapter
    if (-not (Test-Path $dir)) { continue }
    $moves = @()
    foreach ($f in Get-ChildItem $dir -Filter '*.md') {
        if ($f.Name -notmatch '^(\d+)-(.+)\.md$') { continue }
        $slug = $matches[2]
        if (-not $number.ContainsKey($slug)) { continue }
        $target = "$($number[$slug])-$slug.md"
        if ($target -ne $f.Name) { $moves += , @($f.FullName, (Join-Path $dir $target)) }
    }
    foreach ($m in $moves) { Rename-Item -LiteralPath $m[0] -NewName ((Split-Path $m[1] -Leaf) + '.tmp') }
    foreach ($m in $moves) { Rename-Item -LiteralPath ($m[1] + '.tmp') -NewName (Split-Path $m[1] -Leaf) }
    if ($moves.Count) { "$($lang.Name): $($moves.Count) renamed" }
}
foreach ($slug in $ordered) { "$($number[$slug]) $($titles[$slug])" }
