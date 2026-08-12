$ErrorActionPreference = 'Continue'
$dir = 'C:\ProntoPC\StoreApp'
$log = Join-Path $dir 'reinstall-log.txt'
"=== ProntoPC reinstall $(Get-Date) ===" | Set-Content $log
function Log($m) { $m | Tee-Object -FilePath $log -Append }

try {
    $cert = Get-ChildItem Cert:\LocalMachine\My |
            Where-Object { $_.Subject -eq 'CN=D4266722-F7B1-45D4-8B71-CC6C75E7273F' } |
            Select-Object -First 1
    if (-not $cert) { Log "No ProntoPC signing cert found in LocalMachine\My"; Log "RESULT: DONE-ERROR"; exit 1 }
    Log "Signing MSIX with cert $($cert.Thumbprint)..."
    $signtool = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
    (& $signtool sign /fd SHA256 /sm /sha1 $cert.Thumbprint (Join-Path $dir 'ProntoPC.msix') 2>&1 | Out-String).Trim() | ForEach-Object { Log $_ }

    Log "Installing update..."
    Add-AppxPackage -Path (Join-Path $dir 'ProntoPC.msix') -ForceUpdateFromAnyVersion -ErrorAction Stop
    $pkg = Get-AppxPackage -Name '5DServiceSolutionsGlobal.ProntoPC'
    Log "Installed version: $($pkg.Version)"
    Log "RESULT: DONE-OK"
}
catch { Log "ERROR: $($_.Exception.Message)"; Log "RESULT: DONE-ERROR" }
