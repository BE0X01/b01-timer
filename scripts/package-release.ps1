param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$PublishDirectory = (Join-Path $PSScriptRoot '../artifacts/publish'),
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '../artifacts')
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+(?:\.\d+)?(?:-[A-Za-z0-9.-]+)?$') { throw 'Invalid release version.' }
$PublishDirectory = (Resolve-Path $PublishDirectory).Path
New-Item -ItemType Directory -Force -Path $ArtifactDirectory | Out-Null
$ArtifactDirectory = (Resolve-Path $ArtifactDirectory).Path
$exe = Join-Path $PublishDirectory 'B01Timer.exe'
$package = Join-Path $ArtifactDirectory "B01Timer-$Version-windows-x64.zip"
$files = @($exe, (Join-Path $PublishDirectory 'LICENSE'), (Join-Path $PublishDirectory 'THIRD-PARTY-NOTICES.txt'))
foreach ($file in $files) { if (-not (Test-Path $file -PathType Leaf)) { throw "Missing release file: $file" } }
$manifest = Join-Path $PublishDirectory 'SHA256SUMS.txt'
$manifestLines = foreach ($file in $files) { "{0}  {1}" -f (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf) }
$manifestLines | Set-Content -Encoding utf8 $manifest
$files += $manifest
Compress-Archive -LiteralPath $files -DestinationPath $package -CompressionLevel Optimal -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    if ($archive.Entries.Count -ne 4 -or $null -eq $archive.GetEntry('SHA256SUMS.txt')) { throw 'Release archive must contain EXE, both licenses and checksums.' }
    $entry = $archive.GetEntry('B01Timer.exe')
    if ($null -eq $entry) { throw 'Release archive is missing B01Timer.exe.' }
    $stream = $entry.Open()
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try { $archiveExeHash = [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
    if ($archiveExeHash -ne (Get-FileHash $exe -Algorithm SHA256).Hash) { throw 'Archived EXE checksum differs from verified build.' }
} finally { $archive.Dispose() }
$checksums = foreach ($file in @($exe, $package)) {
    "{0}  {1}" -f (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}
$checksums | Set-Content -Encoding utf8 (Join-Path $ArtifactDirectory 'SHA256SUMS.txt')
Write-Output "Verified release package: $package"
