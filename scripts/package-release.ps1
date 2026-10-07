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
# Select only runtime and license files, even if an older publish folder has checksums.
Compress-Archive -LiteralPath $files -DestinationPath $package -CompressionLevel Optimal -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    if ($archive.Entries.Count -ne 3) { throw 'Release archive must contain only EXE and both licenses.' }
    foreach ($file in $files) {
        $name = Split-Path $file -Leaf
        $entry = $archive.GetEntry($name)
        if ($null -eq $entry) { throw "Release archive is missing $name." }
        $stream = $entry.Open()
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try { $archiveHash = [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '') }
        finally { $algorithm.Dispose(); $stream.Dispose() }
        if ($archiveHash -ne (Get-FileHash $file -Algorithm SHA256).Hash) { throw "Archived $name checksum differs from verified build." }
    }
} finally { $archive.Dispose() }
$checksums = foreach ($file in @($exe, $package)) {
    "{0}  {1}" -f (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}
$checksums | Set-Content -Encoding utf8 (Join-Path $ArtifactDirectory 'SHA256SUMS.txt')
Write-Output "Verified release package: $package"
