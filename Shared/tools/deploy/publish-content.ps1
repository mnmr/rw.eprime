# Publishing sends the deployed folder. About.xml can remain unchanged when
# translations are retired or embedded catalogs change, so validate those
# shipped files against mod/ before any network access or confirmation.
function Assert-WorkshopDeploymentContent {
    param([string]$Source, [string]$Deployed)

    function Get-PublishContentManifest([string]$Root) {
        $manifest = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($tree in @('FallbackLanguages', 'Languages', 'Help')) {
            $path = Join-Path $Root $tree
            if (Test-Path -LiteralPath $path) {
                foreach ($file in Get-ChildItem -LiteralPath $path -File -Recurse -Force) {
                    $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
                    $manifest.Add($relative, (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
                }
            }
        }
        # Discover versioned and root Assemblies folders without duplicating
        # supported game versions or inspecting unrelated mod assets.
        foreach ($file in Get-ChildItem -LiteralPath $Root -Filter '*.dll' -File -Recurse -Force) {
            $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
            if ($relative -match '(^|[\\/])Assemblies[\\/]') {
                $manifest.Add($relative, (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
            }
        }
        return ,$manifest
    }

    $expected = Get-PublishContentManifest $Source
    $actual = Get-PublishContentManifest $Deployed
    $recovery = 'The upload sends the deployed folder; run dotnet build + scripts/deploy.ps1 first.'
    foreach ($entry in $expected.GetEnumerator()) {
        if (-not $actual.ContainsKey($entry.Key)) {
            throw "Deployed content is missing $($entry.Key). $recovery"
        }
        if ($entry.Value -ne $actual[$entry.Key]) {
            throw "Deployed content differs from the repo: $($entry.Key). $recovery"
        }
    }
    foreach ($path in $actual.Keys) {
        if (-not $expected.ContainsKey($path)) {
            throw "Deployed content contains an extra file: $path. $recovery"
        }
    }
}
