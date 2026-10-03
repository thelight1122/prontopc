# Run INSIDE the Cert VM as the same account that will test ProntoPC (PreFlight).
$ErrorActionPreference = 'Stop'
$expectedVersion = '1.2.21.0'
$expectedSource = '228BFB022ABEEA9D722B667091F6423C4387950147C4EDA0B2712C25CDD37655'
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
Write-Host 'UPDATE VERIFIED: ProntoPC 1.2.21.0 for the current account.' -ForegroundColor Green
Write-Host 'Launch ProntoPC from Start. The AI/AEGIS disclosure and reporting links must appear at the bottom throughout the scan.'
Write-Host 'Expected path: completed scan -> findings/repair plan -> Technician -> reporting controls.'
Write-Host 'If the retry screen returns, collect both possible plan-authoring logs:'
$paths = @(
    "$env:LOCALAPPDATA\ProntoPC\logs\plan-authoring.log"
    "$env:LOCALAPPDATA\Packages\$($installed.PackageFamilyName)\LocalCache\Local\ProntoPC\logs\plan-authoring.log"
)
foreach ($path in $paths) { Write-Host $path }
