$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$package = Join-Path $repo "StoreApp\ProntoPC.msix"
$report = Join-Path $repo "wack-report-12151.xml"

if (!(Test-Path -LiteralPath $package)) {
    throw "Package not found: $package"
}

$kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit"
$appCert = Join-Path $kitsRoot "appcert.exe"
if (!(Test-Path -LiteralPath $appCert)) {
    throw "Windows App Certification Kit appcert.exe not found at: $appCert"
}

Write-Host "Running WACK for ProntoPC 1.2.15.1"
Write-Host "Package: $package"
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

if ($appVersion -ne "1.2.15.1") {
    Write-Warning "Expected APP_VERSION 1.2.15.1, but WACK report says $appVersion"
}
