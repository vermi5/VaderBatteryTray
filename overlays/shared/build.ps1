$ErrorActionPreference = "Stop"

# PowerShell 5.1's Get-Content/Set-Content silently fall back to the system
# ANSI codepage for BOM-less files unless -Encoding is exactly right, which
# corrupts any non-ASCII character (e.g. the middle dot "·" used in the
# overlay layout). Use .NET's File APIs directly with an explicit
# BOM-less UTF-8 encoding on both the read and the write side instead.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$sharedDirectory = $PSScriptRoot
$corePath = Join-Path $sharedDirectory "battery-overlay-core.js"
$core = [System.IO.File]::ReadAllText($corePath, $utf8NoBom).TrimEnd("`r", "`n")

$targets = @(
    (Join-Path $sharedDirectory "..\obs\vader-battery-overlay.html"),
    (Join-Path $sharedDirectory "..\wallpaper-engine\index.html")
)

$beginMarker = "// BEGIN GENERATED CORE"
$endMarker = "// END GENERATED CORE"

foreach ($target in $targets) {
    $resolvedTarget = (Resolve-Path -LiteralPath $target).Path
    $content = [System.IO.File]::ReadAllText($resolvedTarget, $utf8NoBom)

    $beginIndex = $content.IndexOf($beginMarker)
    $endIndex = $content.IndexOf($endMarker)
    if ($beginIndex -lt 0 -or $endIndex -lt 0 -or $endIndex -lt $beginIndex) {
        throw "Generated-core markers not found in $resolvedTarget"
    }

    $beginContentIndex = $content.IndexOf("`n", $beginIndex) + 1
    $before = $content.Substring(0, $beginContentIndex)
    $after = $content.Substring($endIndex)

    $spliced = $before + $core + "`r`n" + $after
    [System.IO.File]::WriteAllText($resolvedTarget, $spliced, $utf8NoBom)

    Write-Host "Spliced battery-overlay-core.js into $resolvedTarget"
}
