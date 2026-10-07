param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/publish'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
dotnet run --project (Join-Path $repo 'tests/B01Timer.Core.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
dotnet run --project (Join-Path $repo 'tests/B01Timer.Qa.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Independent QA core checks failed.' }
dotnet run --project (Join-Path $repo 'tests/B01Timer.Appearance.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Windows appearance checks failed.' }
dotnet publish (Join-Path $repo 'src/B01Timer/B01Timer.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item (Join-Path $repo 'LICENSE') $OutputDirectory
Copy-Item (Join-Path $repo 'THIRD-PARTY-NOTICES.txt') $OutputDirectory
Get-FileHash (Join-Path $OutputDirectory 'B01Timer.exe') -Algorithm SHA256 | Format-List
