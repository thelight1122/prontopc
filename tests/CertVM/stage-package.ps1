#Requires -RunAsAdministrator
<#
  ProntoPC Cert VM, each run: revert to CLEAN-BASELINE, boot, and copy the candidate
  (test-signed copy, its cert, VCLibs, and the installer) into C:\CertTest in the VM.

  Source: C:\ProntoPC-Sandbox, filled by tests\CleanSandbox\prepare-host.ps1, which signs a
  COPY of the candidate. Run that first. The unsigned Partner Center package is never touched.
#>
param(
    [string]$VmName = 'ProntoPC-CertVM',
    [string]$Staged = 'C:\ProntoPC-Sandbox',
    [switch]$NoRevert
)
$ErrorActionPreference = 'Stop'

$files = 'ProntoPC-test.msix', 'prontopc-test.cer',
         'Microsoft.VCLibs.x64.14.00.Desktop.appx', 'source-sha256.txt'
foreach ($f in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $Staged $f))) {
        throw "Missing $Staged\$f. Run tests\CleanSandbox\prepare-host.ps1 on the candidate first."
    }
}
Write-Host "Candidate: $(Get-Content (Join-Path $Staged 'source-sha256.txt'))"

if (-not $NoRevert) {
    Write-Host "Reverting to CLEAN-BASELINE..."
    Restore-VMCheckpoint -VMName $VmName -Name 'CLEAN-BASELINE' -Confirm:$false
}
if ((Get-VM -Name $VmName).State -ne 'Running') { Start-VM -Name $VmName }

Write-Host "Waiting for the guest heartbeat (sign in to the VM if it waits at the lock screen)..."
$deadline = (Get-Date).AddMinutes(5)
while ((Get-VMIntegrationService -VMName $VmName -Name Heartbeat).PrimaryStatusDescription -ne 'OK') {
    if ((Get-Date) -gt $deadline) { throw "Guest did not come up within 5 minutes." }
    Start-Sleep -Seconds 5
}

$sources = @{}
foreach ($f in $files) { $sources[$f] = Join-Path $Staged $f }
$sources['install-in-vm.ps1'] = Join-Path $PSScriptRoot 'install-in-vm.ps1'
foreach ($f in $sources.Keys) {
    Copy-VMFile -Name $VmName -SourcePath $sources[$f] -DestinationPath "C:\CertTest\$f" `
                -FileSource Host -CreateFullPath -Force
    Write-Host "  copied $f"
}
vmconnect.exe localhost $VmName
Write-Host @"

In the VM, as CertAdmin in an elevated PowerShell:
  powershell -ExecutionPolicy Bypass -File C:\CertTest\install-in-vm.ps1
Then sign out, sign in as CertUser, and do the run in README.md.
"@
