# Exports the ASP.NET Core HTTPS development certificate for the Compose `full` profile (plan T0.11, P19).
#   powershell -ExecutionPolicy Bypass -File scripts/dev-cert.ps1
# Writes certs/fixflow-dev.pem and certs/fixflow-dev.key (untracked). The key has no password: the certificate is for
# localhost only, and the API container reads it from a read-only bind mount.
# Run it again when the certificate expires (yearly) or after `dotnet dev-certs https --clean`.

$ErrorActionPreference = 'Stop'

$certs = Join-Path (Split-Path -Parent $PSScriptRoot) 'certs'
New-Item -ItemType Directory -Force -Path $certs | Out-Null

# Creates the certificate if there is none and trusts it for the current user (Windows asks to confirm). Without
# trust the API still runs; the browser shows a warning for https://localhost:8443.
dotnet dev-certs https --trust
if ($LASTEXITCODE -ne 0) { Write-Warning 'The development certificate could not be trusted.' }

dotnet dev-certs https --export-path (Join-Path $certs 'fixflow-dev.pem') --format Pem --no-password
if ($LASTEXITCODE -ne 0) { throw 'Exporting the development certificate failed.' }

Write-Host "Exported to $certs"
