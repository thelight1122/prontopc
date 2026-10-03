# Run in ordinary PowerShell INSIDE the Cert VM as PreFlight.
# Read-only package inspection; writes a new evidence JSON to the current user's Desktop.
$ErrorActionPreference = 'Stop'
$expectedDllHash = '9DD5B9E8E9646BDCDCF145A229E098F9B95CA1642628DE8FF3B92E0EA240AF68'
$observedUtc = [DateTime]::UtcNow.ToString('o')
$package = Get-AppxPackage -Name '5DServiceSolutionsGlobal.ProntoPC'
$evidence = [ordered]@{
    observed_utc = $observedUtc
    observer_account = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    machine = $env:COMPUTERNAME
    candidate_version = '1.2.21.0'
    archived_upload_sha256 = '8D2FF6B1867FE22BE1612A239B906F80E931EBD9BBB6ED48993DC1B36CFD82C3'
    archived_unsigned_msix_sha256 = '228BFB022ABEEA9D722B667091F6423C4387950147C4EDA0B2712C25CDD37655'
    expected_candidate_dll_sha256 = $expectedDllHash
    installed_identity = $null
    installed_dll_version = $null
    installed_dll_sha256 = $null
    identity_and_dll_binding = 'untested'
    shortfall_reason = $null
    installed_reporting_flow = 'untested'
    support_receipt = 'untested'
    support_monitoring = 'untested'
    full_installed_payload_equivalence = 'untested; DLL binding alone does not establish all package bytes'
}
try {
    if (-not $package) { throw 'No ProntoPC package installed for this account.' }
    $evidence.installed_identity = [ordered]@{
        name = $package.Name; version = $package.Version
        package_full_name = $package.PackageFullName
        install_location = $package.InstallLocation
    }
    if ($package.Version -ne '1.2.21.0') { throw "Installed version is $($package.Version); do not test another version as this candidate." }
    $dll = Join-Path $package.InstallLocation 'ProntoPC.dll'
    $evidence.installed_dll_version = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
    $evidence.installed_dll_sha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    if ($evidence.installed_dll_version -ne '1.2.21.0' -or $evidence.installed_dll_sha256 -ne $expectedDllHash) {
        throw 'Installed DLL differs from the preserved reporting candidate.'
    }
    $evidence.identity_and_dll_binding = 'PASS'
} catch {
    $evidence.identity_and_dll_binding = 'shortfall'
    $evidence.shortfall_reason = $_.Exception.Message
}
$output = Join-Path ([Environment]::GetFolderPath('Desktop')) ('ProntoPC-12210-identity-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json')
if (Test-Path -LiteralPath $output) { throw "Evidence already exists: $output" }
$evidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $output -Encoding UTF8
$evidence | ConvertTo-Json -Depth 6 | Write-Output
Write-Host "Saved evidence: $output"
if ($evidence.identity_and_dll_binding -ne 'PASS') {
    Write-Host 'STOP this candidate test. No install, downgrade or trust change was attempted.' -ForegroundColor Yellow
    exit 1
}
Write-Host 'Candidate identity and DLL binding PASS. Reporting, mailbox receipt and monitoring remain UNTESTED.' -ForegroundColor Green
Write-Host 'Use the reporting checklist; this script does not launch the app, send mail or prove runtime acceptance.'
