param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$sourceFile = Join-Path $repoRoot 'VaderBatteryTray\VaderBatteryTray.cs'
$applicationDirectory = Join-Path $repoRoot 'VaderBatteryTray'
$applicationPath = Join-Path $applicationDirectory 'VaderBatteryTray.exe'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $source = Get-Content -LiteralPath $sourceFile -Raw
    $match = [regex]::Match($source, 'AssemblyFileVersion\("(\d+\.\d+\.\d+)\.0"\)')
    if (-not $match.Success) {
        throw 'Could not determine the package version from AssemblyFileVersion.'
    }
    $Version = $match.Groups[1].Value
}

if (-not (Test-Path -LiteralPath $applicationPath)) {
    throw 'Build VaderBatteryTray.exe before packaging.'
}

$outputDirectory = Join-Path $repoRoot 'release'
$stageDirectory = Join-Path $outputDirectory ('VaderBatteryTray-' + $Version)
$zipPath = Join-Path $outputDirectory ('VaderBatteryTray-' + $Version + '.zip')
$checksumPath = $zipPath + '.sha256'

if (Test-Path -LiteralPath $stageDirectory) {
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$files = @(
    'CHANGELOG.md',
    'LICENSE',
    'README.md'
)
foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination $stageDirectory -Force
}

$applicationFiles = @(
    'Install Startup Shortcut.cmd',
    'RAINMETER_BRIDGE.md',
    'Remove Startup Shortcut.cmd',
    'VaderBatteryTray.cmd',
    'VaderBatteryTray.exe'
)
foreach ($file in $applicationFiles) {
    Copy-Item -LiteralPath (Join-Path $applicationDirectory $file) -Destination $stageDirectory -Force
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'rainmeter') -Destination $stageDirectory -Recurse -Force

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -LiteralPath $stageDirectory -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content -LiteralPath $checksumPath -Value ($hash + ' *' + [IO.Path]::GetFileName($zipPath)) -NoNewline
Write-Output ('Package: ' + $zipPath)
Write-Output ('SHA256: ' + $hash)
