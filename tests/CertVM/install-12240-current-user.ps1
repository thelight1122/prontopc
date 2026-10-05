# Run INSIDE the Cert VM as the same account that will test ProntoPC (PreFlight).
$ErrorActionPreference = 'Stop'
$expectedVersion = '1.2.24.0'
$expectedSource = 'F150F2982E82896F953579EDF889F73DD2E901FAF353F30E0D60DC3B2D0A2048'
$identity = '5DServiceSolutionsGlobal.ProntoPC'
if ((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'source-sha256.txt') -Raw).Trim() -ne $expectedSource) { throw 'Source binding mismatch.' }
$package = Join-Path $PSScriptRoot 'ProntoPC-test.msix'
$signedHash = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'signed-sha256.txt') -Raw).Trim()
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $signedHash) { throw 'Signed test-copy hash mismatch.' }
$thumbprint = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'test-cert-thumbprint.txt') -Raw).Trim()
$trusted = @(
    Get-ChildItem Cert:\LocalMachine\TrustedPeople
    Get-ChildItem Cert:\CurrentUser\TrustedPeople
) | Where-Object Thumbprint -eq $thumbprint
if (-not $trusted) { throw 'The test certificate is not already trusted in this VM. No trust was changed; stop and report this message.' }
$running = Get-Process -Name ProntoPC -ErrorAction SilentlyContinue
if ($running) { throw 'Close the ProntoPC window before updating. This script will not kill it.' }
$installed = Get-AppxPackage -Name $identity
if (-not $installed) { throw 'No existing ProntoPC package for this account. Run this as the PreFlight account that has 1.2.20.0.' }
if ([version]$installed.Version -gt [version]$expectedVersion) { throw 'A newer package is installed. No downgrade attempted.' }
if ([version]$installed.Version -lt [version]$expectedVersion) {
    Add-AppxPackage -Path $package -ErrorAction Stop
}
$installed = Get-AppxPackage -Name $identity
if ($installed.Version -ne $expectedVersion) { throw "Unexpected installed version: $($installed.Version)" }
$installedDll = Join-Path $installed.InstallLocation 'ProntoPC.dll'
$installedVersion = [Reflection.AssemblyName]::GetAssemblyName($installedDll).Version.ToString()
if ($installedVersion -ne $expectedVersion) { throw "Installed DLL version mismatch: $installedVersion" }
$installed | Select-Object Name,Version,InstallLocation
Write-Host 'UPDATE VERIFIED: ProntoPC 1.2.24.0 for the current account.' -ForegroundColor Green
Write-Host 'Combined release: persistent reporting footer and awaited model preparation. Open from Start and verify the first scan reaches findings without Retry.'
Write-Host 'Then test reporting the displayed repair-plan summary and one actual Technician reply; review the report text before sending.'

