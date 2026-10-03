#Requires -RunAsAdministrator
<#
  ProntoPC Cert VM, step 1: create a clean Hyper-V Windows 11 VM that behaves like a
  Microsoft cert device (standard user, UAC on, no VC++ redist, no GPU, no dev tools).

  Run from an ELEVATED PowerShell in this folder:  .\new-certvm.ps1
  Then install Windows in the VM window (see README.md), and run .\baseline-certvm.ps1.
  Guard: refuses to touch a VM that already exists with the same name.
#>
param(
    [string]$Iso      = 'D:\DOWNLOADS FOLDER\Win11_24H2_English_x64.iso',
    [string]$VmName   = 'ProntoPC-CertVM',
    [string]$VmRoot   = 'H:\HyperV\ProntoPC-CertVM',
    [int]   $Cpu      = 4,
    [long]  $MemoryGB = 8,
    [long]  $DiskGB   = 128,
    [string]$Switch   = 'Default Switch'
)
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Iso)) { throw "ISO not found: $Iso" }
if (Get-VM -Name $VmName -ErrorAction SilentlyContinue) {
    throw "A VM named '$VmName' already exists. Remove or rename it first (nothing was changed)."
}
if (-not (Get-VMSwitch -Name $Switch -ErrorAction SilentlyContinue)) {
    throw "Virtual switch '$Switch' not found. Available: $((Get-VMSwitch).Name -join ', ')"
}

New-Item -ItemType Directory -Force -Path $VmRoot | Out-Null
$vhd = Join-Path $VmRoot "$VmName.vhdx"

Write-Host "Creating Generation 2 VM '$VmName' in $VmRoot"
New-VM -Name $VmName -Generation 2 -Path $VmRoot `
       -MemoryStartupBytes ($MemoryGB * 1GB) `
       -NewVHDPath $vhd -NewVHDSizeBytes ($DiskGB * 1GB) `
       -SwitchName $Switch | Out-Null

# Static memory + fixed CPU count: predictable, closer to a real laptop than dynamic memory.
Set-VMMemory    -VMName $VmName -DynamicMemoryEnabled $false
Set-VMProcessor -VMName $VmName -Count $Cpu

# Windows 11 requirements: Secure Boot (Microsoft Windows template) + a virtual TPM.
Set-VMFirmware     -VMName $VmName -EnableSecureBoot On -SecureBootTemplate 'MicrosoftWindows'
Set-VMKeyProtector -VMName $VmName -NewLocalKeyProtector
Enable-VMTPM       -VMName $VmName

# Install media, booted first.
$dvd = Add-VMDvdDrive -VMName $VmName -Path $Iso -Passthru
Set-VMFirmware -VMName $VmName -FirstBootDevice $dvd

# Guest services let Copy-VMFile push packages in later (stage-package.ps1).
Enable-VMIntegrationService -VMName $VmName -Name 'Guest Service Interface'

# No automatic checkpoints: we take named ones on purpose.
Set-VM -Name $VmName -AutomaticCheckpointsEnabled $false -CheckpointType Standard

# Deliberately NO GPU partition: the model runs on CPU, and cert devices promise no GPU.

Write-Host "VM created. Starting it and opening the console..."
Start-VM -Name $VmName
vmconnect.exe localhost $VmName

Write-Host @"

NEXT (in the VM window):
  1. Press a key when it says 'Press any key to boot from CD or DVD'.
  2. Install Windows 11 Pro. Create the ADMIN account first (CertAdmin).
  3. Do NOT install Visual Studio, the VC++ redistributable, or any dev tools.
  4. Create the standard user, then run .\baseline-certvm.ps1 on the host.
"@
