#Requires -RunAsAdministrator
<#
  ProntoPC Cert VM, step 2: after Windows is installed and the STANDARD user exists in the VM,
  eject the ISO, shut down, and take the CLEAN-BASELINE checkpoint.
#>
param([string]$VmName = 'ProntoPC-CertVM')
$ErrorActionPreference = 'Stop'

$vm = Get-VM -Name $VmName
if (Get-VMCheckpoint -VMName $VmName -Name 'CLEAN-BASELINE' -ErrorAction SilentlyContinue) {
    throw "CLEAN-BASELINE already exists. Nothing changed."
}
Get-VMDvdDrive -VMName $VmName | Set-VMDvdDrive -Path $null
if ($vm.State -ne 'Off') {
    Write-Host "Shutting the VM down cleanly for the baseline..."
    Stop-VM -Name $VmName
}
Checkpoint-VM -Name $VmName -SnapshotName 'CLEAN-BASELINE'
Write-Host "CLEAN-BASELINE checkpoint taken."
Get-VMCheckpoint -VMName $VmName | Select-Object Name, CreationTime
