<#
.SYNOPSIS
    Builds the Windows x64 distribution ZIP of Hotel Reservation System.

.DESCRIPTION
    Publishes a self-contained, single-file executable (no .NET runtime needed on the target
    machine) and packages it with appsettings.json, the database scripts, the license and
    installation notes:

        artifacts\HotelReservationSystem-v<version>-win-x64.zip
        artifacts\HotelReservationSystem-v<version>-win-x64.zip.sha256

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\publish.ps1
#>
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$artifacts = Join-Path $root "artifacts"
$name = "HotelReservationSystem-v$Version-win-x64"
$staging = Join-Path $artifacts $name
$zip = Join-Path $artifacts "$name.zip"

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force $staging | Out-Null

Write-Host "Publishing $name ..."
dotnet publish (Join-Path $root "src\HotelReservationSystem\HotelReservationSystem.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:Version=$Version `
    -o $staging
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

New-Item -ItemType Directory -Force (Join-Path $staging "database") | Out-Null
Copy-Item (Join-Path $root "database\*") (Join-Path $staging "database")
Copy-Item (Join-Path $root "LICENSE") (Join-Path $staging "LICENSE.txt")
Copy-Item (Join-Path $root "build\INSTALL.txt") (Join-Path $staging "INSTALL.txt")

Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $name.zip" | Set-Content "$zip.sha256" -Encoding ASCII

Write-Host ""
Write-Host "Package: $zip"
Write-Host "SHA-256: $hash"
Get-ChildItem $staging | Select-Object Name, @{ n = "Size (MB)"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
