<#
.SYNOPSIS
    Creates the HotelReservationDB database and (optionally) loads the fictional demo data.

.EXAMPLE
    .\setup-database.ps1                                # .\SQLEXPRESS, schema + demo data
    .\setup-database.ps1 -Server "(localdb)\MSSQLLocalDB"
    .\setup-database.ps1 -SkipSeed                      # empty database, first-run setup creates the admin
#>
param(
    [string]$Server = ".\SQLEXPRESS",
    [switch]$SkipSeed
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error "sqlcmd was not found. Install SQL Server command line tools: https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility"
}

function Invoke-Script([string]$file) {
    Write-Host "Running $file on $Server ..."
    & sqlcmd -S $Server -E -b -C -f 65001 -i (Join-Path $here $file)
    if ($LASTEXITCODE -ne 0) { throw "$file failed (exit code $LASTEXITCODE)." }
}

Invoke-Script "schema.sql"
if (-not $SkipSeed) { Invoke-Script "seed.sql" }

Write-Host ""
Write-Host "Done. Connection string for appsettings.json:"
Write-Host "  Server=$Server;Database=HotelReservationDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
