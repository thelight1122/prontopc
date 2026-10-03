$ErrorActionPreference = 'Stop'
$packagePath = Join-Path $PSScriptRoot 'ProntoPC-12200-test.msix'
$expectedHash = 'C206BCEF78801984D5D6A1DA829DA15251A32FAE725B1FD3946B0A0D730F218E'
if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Baseline package hash mismatch. No installation performed.'
}
if (Get-Process -Name ProntoPC -ErrorAction SilentlyContinue) {
    throw 'Close ProntoPC before restoring the baseline.'
}
if (-not (Get-Command Add-AppxPackage).Parameters.ContainsKey('ForceUpdateFromAnyVersion')) {
    throw 'This Windows version does not expose the required package downgrade option.'
}
Write-Output ('Restoring baseline for current account: ' + [Security.Principal.WindowsIdentity]::GetCurrent().Name)
# User-authorized rollback. Preserve package data; no uninstall, VM reset or trust change.
Add-AppxPackage -Path $packagePath -ForceUpdateFromAnyVersion -ErrorAction Stop
$installed = Get-AppxPackage -Name '5DServiceSolutionsGlobal.ProntoPC'
if (-not $installed -or $installed.Version -ne [version]'1.2.20.0') {
    throw 'Expected installed version 1.2.20.0 after restoration.'
}
$installed | Select-Object Name, Version, InstallLocation
Write-Output 'BASELINE RESTORED. Open ProntoPC normally and verify scan -> AI repair plan. No disclaimer/reporting footer is expected.'
