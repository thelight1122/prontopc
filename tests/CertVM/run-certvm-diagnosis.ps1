#Requires -RunAsAdministrator
<#
  ProntoPC Cert VM: one run, end to end. Everything except the final clicks as CertUser.

    cd 'H:\PRONTOPC_PRODUCTION\.claude\worktrees\prontopc-cert-ready-status-c443cd\tests\CertVM'; .\run-certvm-diagnosis.ps1

  What it does, in order (it stops on the first problem and says which step):
    1. Asks you for the VM's ADMIN sign-in (a Windows prompt; your password never leaves the prompt).
    2. Inside the VM: records the build, checks no VC++ runtime is in System32, creates the standard
       user CertUser if it is missing (asks you to choose its password).
    3. Takes the CLEAN-BASELINE checkpoint if there isn't one yet.
    4. Signs a test COPY of StoreApp\ProntoPC.msix (the Partner Center package is never touched).
    5. Reverts to CLEAN-BASELINE, boots, creates CertUser if needed, copies the package in, installs it for all users.
    6. Writes everything to C:\CertTest\run.log in the VM and prints it here.
  Then you sign in as CertUser and click: Scan -> Findings -> Perform Repairs.

  -AsAdmin: the reviewer-like run. Same clean install, but you test as the ADMIN account and
  approve the UAC prompt with "Yes" (no password). CertUser is not created or needed.
    .\run-certvm-diagnosis.ps1 -AsAdmin
#>
param(
    [string]$VmName  = 'ProntoPC-CertVM',
    [string]$Package = 'H:\PRONTOPC_PRODUCTION\StoreApp\ProntoPC.msix',
    [string]$Prepare = 'H:\PRONTOPC_PRODUCTION\.claude\worktrees\prontopc-cert-rejection-54139f\tests\CleanSandbox\prepare-host.ps1',
    [switch]$AsAdmin
)
$ErrorActionPreference = 'Stop'
function Step($n, $text) { Write-Host "`n[$n] $text" -ForegroundColor Cyan }

function Wait-Guest {
    $deadline = (Get-Date).AddMinutes(6)
    while ($true) {
        try { Invoke-Command -VMName $VmName -Credential $script:cred -ScriptBlock { 'up' } -ErrorAction Stop | Out-Null; return }
        catch { if ((Get-Date) -gt $deadline) { throw "The VM did not accept a sign-in within 6 minutes: $_" } ; Start-Sleep 5 }
    }
}

Step 0 "Checking the VM"
$vm = Get-VM -Name $VmName
if ($vm.State -ne 'Running') { Start-VM -Name $VmName }
foreach ($p in $Package, $Prepare) { if (-not (Test-Path -LiteralPath $p)) { throw "Not found: $p" } }

Step 1 "Sign in with the VM's ADMIN account (the one you made during Windows setup)"
$script:cred = Get-Credential -Message "Admin account INSIDE $VmName"
Wait-Guest

Step 2 "Inspecting the VM"
$userPw = $null
if (-not $AsAdmin) { $userPw = Read-Host -AsSecureString "Choose/enter the password to use for standard user CertUser after CLEAN-BASELINE restore" }
$facts = Invoke-Command -VMName $VmName -Credential $cred -ScriptBlock {
    $v = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    [pscustomobject]@{
        Build      = "$($v.DisplayVersion) $($v.CurrentBuild).$($v.UBR)"
        VcRuntime  = Test-Path "$env:WINDIR\System32\vcruntime140.dll"
        CertUserExists = [bool](Get-LocalUser -Name CertUser -ErrorAction SilentlyContinue)
        CertUserIsAdmin = [bool](Get-LocalGroupMember Administrators -ErrorAction SilentlyContinue | Where-Object Name -like '*\CertUser')
        UAC        = (Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System).EnableLUA
    }
}
$facts | Format-List | Out-String | Write-Host
if ($facts.VcRuntime)       { throw "vcruntime140.dll is in System32: this VM is not clean (VC++ runtime installed)." }
if ($facts.CertUserIsAdmin) { throw "CertUser is an administrator; it must be a standard user." }
if ($facts.UAC -ne 1)       { throw "UAC is off in the VM; turn it on so elevation is really tested." }
if (-not $AsAdmin -and -not $userPw) { throw "CertUser password is required for the standard-user Cert VM run." }

Step 3 "CLEAN-BASELINE checkpoint"
if (-not (Get-VMCheckpoint -VMName $VmName -Name 'CLEAN-BASELINE' -ErrorAction SilentlyContinue)) {
    Get-VMDvdDrive -VMName $VmName | Set-VMDvdDrive -Path $null
    Stop-VM -Name $VmName
    Checkpoint-VM -Name $VmName -SnapshotName 'CLEAN-BASELINE'
    Write-Host "Taken."
} else { Write-Host "Already exists; using it." }

Step 4 "Signing a test copy of the package"
& $Prepare -Msix $Package -Label FIX

Step 5 "Reverting to CLEAN-BASELINE, booting, creating CertUser, copying, installing"
Restore-VMCheckpoint -VMName $VmName -Name 'CLEAN-BASELINE' -Confirm:$false
Start-VM -Name $VmName
Wait-Guest
if (-not $AsAdmin) {
    Invoke-Command -VMName $VmName -Credential $cred -ArgumentList $userPw -ScriptBlock {
        param($pw)
        $existing = Get-LocalUser -Name CertUser -ErrorAction SilentlyContinue
        if (-not $existing) {
            New-LocalUser -Name CertUser -Password $pw -AccountNeverExpires | Out-Null
            Add-LocalGroupMember -Group Users -Member CertUser
            "Created standard user CertUser after CLEAN-BASELINE restore."
        } else {
            Set-LocalUser -Name CertUser -Password $pw
            Enable-LocalUser -Name CertUser
            if (Get-LocalGroupMember Administrators -ErrorAction SilentlyContinue | Where-Object Name -like '*\CertUser') {
                throw "CertUser is an administrator after restore; remove it from Administrators before testing."
            }
            "CertUser already existed after CLEAN-BASELINE restore; password refreshed and account enabled."
        }
        $profile = Join-Path 'C:\Users' 'CertUser'
        [pscustomobject]@{
            CertUserExists = [bool](Get-LocalUser -Name CertUser -ErrorAction SilentlyContinue)
            CertUserIsAdmin = [bool](Get-LocalGroupMember Administrators -ErrorAction SilentlyContinue | Where-Object Name -like '*\CertUser')
            ProfileExistsBeforeSignIn = Test-Path -LiteralPath $profile
        }
    } | Format-List | Out-String | Write-Host
}
$stage = 'C:\ProntoPC-Sandbox'
$files = @{
    'ProntoPC-test.msix'                     = "$stage\ProntoPC-test.msix"
    'prontopc-test.cer'                      = "$stage\prontopc-test.cer"
    'Microsoft.VCLibs.x64.14.00.Desktop.appx'= "$stage\Microsoft.VCLibs.x64.14.00.Desktop.appx"
    'source-sha256.txt'                      = "$stage\source-sha256.txt"
    'install-in-vm.ps1'                      = "$PSScriptRoot\install-in-vm.ps1"
}
$session = New-PSSession -VMName $VmName -Credential $cred
try {
    Invoke-Command -Session $session -ScriptBlock { New-Item -ItemType Directory -Force C:\CertTest | Out-Null }
    foreach ($name in $files.Keys) { Copy-Item -ToSession $session -Path $files[$name] -Destination "C:\CertTest\$name" -Force; Write-Host "  copied $name" }
    $log = Invoke-Command -Session $session -ScriptBlock {
        & powershell -NoProfile -ExecutionPolicy Bypass -File C:\CertTest\install-in-vm.ps1 *>&1 | Out-String | Tee-Object C:\CertTest\run.log
    }
} finally { Remove-PSSession $session }

Step 6 "Install log from inside the VM"
Write-Host $log
vmconnect.exe localhost $VmName
if ($AsAdmin) {
    Write-Host @"

READY (ADMIN RUN). In the VM window:
  1. Sign out, then sign back in as the ADMIN account (this picks up the all-users install).
  2. Start -> ProntoPC -> Scan -> wait for Findings -> Perform Repairs.
  3. When UAC asks, click YES. There is no password on this path; that is the point of this run.
  4. If '0xc0000142' appears, screenshot it, and note whether it came at launch or at Perform Repairs.
"@ -ForegroundColor Green
    return
}
Write-Host @"

READY. In the VM window:
  1. Sign out, then sign in as CertUser.
  2. Start -> ProntoPC -> Scan -> wait for Findings -> Perform Repairs.
  3. When UAC asks, type the ADMIN account's password.
  4. If '0xc0000142' appears, screenshot it. That confirms the diagnosis.
"@ -ForegroundColor Green
