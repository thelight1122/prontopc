#Requires -RunAsAdministrator
# Run from an elevated PowerShell window. Tests only the exact prepared package.
$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$package = Join-Path $repo 'StoreApp\ProntoPC.msix'
$upload = Join-Path $repo 'StoreApp\ProntoPC.msixupload'
$expectedVersion = '1.2.26.0'
$expectedSha256 = '5655EA7DA7C970217A9FF65ADECDA317E2432A715F9DF89AA8778EABCFB09185'
$expectedUploadSha256 = 'B25026F529249CC32734A85E34C0F2446EE2EB8126D9D0B1D63F6DA35025A4AD'
$report = Join-Path $repo ('wack-report-12260-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.xml')
$canonicalReport = Join-Path $repo 'wack-report-12260.xml'
$currentReport = Join-Path $repo 'wack-report-current.xml'
$appCert = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\App Certification Kit\appcert.exe'

foreach ($item in @($package, $upload, $appCert)) {
    if (-not (Test-Path -LiteralPath $item -PathType Leaf)) { throw "Required file missing: $item" }
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedSha256) {
    throw 'MSIX hash mismatch. This script is bound to the prepared 1.2.26.0 package; do not test a replacement with this script.'
}
if ((Get-FileHash -LiteralPath $upload -Algorithm SHA256).Hash -ne $expectedUploadSha256) {
    throw 'MSIXUPLOAD hash mismatch. Stop and rebind the release evidence.'
}
if (Test-Path -LiteralPath $report) { throw "Report already exists: $report" }

Write-Host "Running WACK for ProntoPC $expectedVersion" -ForegroundColor Cyan
Write-Host "Package: $package"
Write-Host "SHA-256: $expectedSha256"
Write-Host "Report: $report"
Write-Host 'Let WACK finish; do not interact with the application during automated testing.'
$startedUtc = [DateTime]::UtcNow
& $appCert reset
if ($LASTEXITCODE -ne 0) { throw "WACK reset failed: $LASTEXITCODE" }
& $appCert test -appxpackagepath $package -reportoutputpath $report
$testExitCode = $LASTEXITCODE
if (-not (Test-Path -LiteralPath $report -PathType Leaf)) {
    throw "WACK produced no report (exit $testExitCode). Expected: $report"
}
if ((Get-Item -LiteralPath $report).LastWriteTimeUtc -lt $startedUtc.AddSeconds(-2)) {
    throw 'WACK report is stale; it will not be promoted.'
}
[xml]$xml = Get-Content -LiteralPath $report -Raw
$overall = [string]$xml.REPORT.OVERALL_RESULT
$appVersion = [string]$xml.REPORT.APP_VERSION
Write-Host "OVERALL_RESULT: $overall"
Write-Host "APP_VERSION: $appVersion"
if ($testExitCode -ne 0 -or $overall -ne 'PASS' -or $appVersion -ne $expectedVersion) {
    throw "WACK did not produce a valid PASS for $expectedVersion (exit=$testExitCode, result=$overall, version=$appVersion). Report retained: $report"
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedSha256 -or
    (Get-FileHash -LiteralPath $upload -Algorithm SHA256).Hash -ne $expectedUploadSha256) {
    throw 'Package bytes changed during WACK. Report retained but not promoted.'
}
Copy-Item -LiteralPath $report -Destination $canonicalReport -Force
Copy-Item -LiteralPath $report -Destination $currentReport -Force
Write-Host "WACK PASS for ProntoPC $expectedVersion" -ForegroundColor Green
Write-Host "Saved: $canonicalReport"
Write-Host 'Installed reporting-flow and support-mailbox verification remain separate from WACK.'

