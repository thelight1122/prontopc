$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$package = Join-Path $repo "StoreApp\ProntoPC.msix"
$report = Join-Path $repo "wack-report-12170.xml"
$expectedVersion = "1.2.17.0"
$expectedSha256 = "317452962A75F03AC2407BCFB9BEA261FFDDC8EFFA31A11614E6CD7A401C865E"

if (!(Test-Path -LiteralPath $package)) {
    throw "Package not found: $package"
}

$actualSha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "Package hash mismatch. Expected $expectedSha256 but found $actualSha256"
}

$kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit"
$appCert = Join-Path $kitsRoot "appcert.exe"
if (!(Test-Path -LiteralPath $appCert)) {
    throw "Windows App Certification Kit appcert.exe not found at: $appCert"
}

Write-Host "Running WACK for ProntoPC $expectedVersion"
Write-Host "Package: $package"
Write-Host "SHA-256: $actualSha256"
Write-Host "Report:  $report"

& $appCert reset
& $appCert test -appxpackagepath $package -reportoutputpath $report

if (!(Test-Path -LiteralPath $report)) {
    throw "WACK report was not created: $report"
}

[xml]$xml = Get-Content -LiteralPath $report
$overall = $xml.REPORT.OVERALL_RESULT
$appVersion = $xml.REPORT.APP_VERSION

Write-Host "OVERALL_RESULT: $overall"
Write-Host "APP_VERSION:    $appVersion"

if ($appVersion -ne $expectedVersion) {
    Write-Warning "Expected APP_VERSION $expectedVersion, but WACK report says $appVersion"
}

if ($overall -ne "PASS") {
    throw "WACK did not pass. See report: $report"
}
