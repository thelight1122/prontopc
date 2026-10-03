# Run from an elevated PowerShell window.

$AppCert = "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe"
$Package = "H:\PRONTOPC_PRODUCTION\StoreApp\ProntoPC.msix"
$Report = "H:\PRONTOPC_PRODUCTION\wack-report-1214.xml"

if (-not (Test-Path -LiteralPath $AppCert)) {
    throw "appcert.exe not found: $AppCert"
}

if (-not (Test-Path -LiteralPath $Package)) {
    throw "MSIX package not found: $Package"
}

if (Test-Path -LiteralPath $Report) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $archiveName = "wack-report-1214-$stamp.xml"
    Rename-Item -LiteralPath $Report -NewName $archiveName
}

& $AppCert test -appxpackagepath $Package -reportoutputpath $Report
$exitCode = $LASTEXITCODE

if (-not (Test-Path -LiteralPath $Report)) {
    throw "WACK did not produce the expected report: $Report"
}

[xml]$doc = Get-Content -LiteralPath $Report
$result = $doc.REPORT.OVERALL_RESULT
$appVersion = $doc.REPORT.APP_VERSION

Write-Host "WACK result: $result"
Write-Host "APP_VERSION: $appVersion"
Write-Host "Report: $Report"

if ($appVersion -ne "1.2.14.0") {
    Write-Warning "Expected APP_VERSION 1.2.14.0, but WACK report says $appVersion"
    exit 2
}

if ($exitCode -ne 0 -or $result -ne "PASS") {
    exit 1
}

exit 0
