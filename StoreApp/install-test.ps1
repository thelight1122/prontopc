$ErrorActionPreference = 'Continue'
$dir = 'C:\ProntoPC\StoreApp'
$log = Join-Path $dir 'install-log.txt'
"=== ProntoPC MSIX sign + install  $(Get-Date) ===" | Set-Content $log
function Log($m) { $m | Tee-Object -FilePath $log -Append }

$subject = 'CN=D4266722-F7B1-45D4-8B71-CC6C75E7273F'   # must match Package Identity Publisher
$msix    = Join-Path $dir 'ProntoPC.msix'
$cer     = Join-Path $dir 'ProntoPC-test.cer'

try {
    Log "Creating self-signed code-signing cert (subject = package publisher)..."
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
                -CertStoreLocation 'Cert:\LocalMachine\My' -FriendlyName 'ProntoPC Test Cert' `
                -NotAfter (Get-Date).AddMonths(3)
    Log "  thumbprint: $($cert.Thumbprint)"

    Log "Trusting the cert (LocalMachine Root + TrustedPeople)..."
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
    Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\Root'         | Out-Null
    Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

    Log "Signing the MSIX..."
    $signtool = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
    $out = & $signtool sign /fd SHA256 /sm /sha1 $cert.Thumbprint $msix 2>&1
    Log ($out | Out-String).Trim()

    Log "Installing the package..."
    Add-AppxPackage -Path $msix -ErrorAction Stop
    $pkg = Get-AppxPackage -Name '5DServiceSolutionsGlobal.ProntoPC'
    Log "  Installed: $($pkg.PackageFullName)"
    Log "  InstallLocation: $($pkg.InstallLocation)"

    $proto = Get-Item 'Registry::HKEY_CLASSES_ROOT\prontopc' -ErrorAction SilentlyContinue
    Log "  prontopc:// protocol registered: $($null -ne $proto)"
    Log "RESULT: DONE-OK"
}
catch {
    Log "ERROR: $($_.Exception.Message)"
    Log "RESULT: DONE-ERROR"
}
