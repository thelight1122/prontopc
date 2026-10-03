<#
  Runs INSIDE the Cert VM as CertAdmin (elevated). Logs the environment, trusts the test cert
  machine-wide, and provisions VCLibs + ProntoPC for ALL users so CertUser gets it at sign-in.
#>
$ErrorActionPreference = 'Stop'
$d = 'C:\CertTest'
Start-Transcript -Path (Join-Path $d ("install-{0:yyyyMMddTHHmmss}.log" -f (Get-Date))) | Out-Null

"Candidate: $(Get-Content "$d\source-sha256.txt")"
"OS build: $([Environment]::OSVersion.Version)"
foreach ($dll in 'msvcp140.dll','msvcp140_1.dll','vcruntime140.dll','vcruntime140_1.dll') {
    "System32 ${dll}: $(Test-Path "$env:WINDIR\System32\$dll")"
}
"UAC EnableLUA: $((Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System).EnableLUA)"

Import-Certificate -FilePath "$d\prontopc-test.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Add-AppxProvisionedPackage -Online -PackagePath "$d\Microsoft.VCLibs.x64.14.00.Desktop.appx" -SkipLicense | Out-Null
Add-AppxProvisionedPackage -Online -PackagePath "$d\ProntoPC-test.msix" -SkipLicense | Out-Null
"Provisioned: $((Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*ProntoPC*').PackageName)"
"NEXT: sign out, sign in as CertUser, launch ProntoPC from Start."
Stop-Transcript | Out-Null
