#Requires -RunAsAdministrator
<#+
  Collects CertUser evidence from the ProntoPC Cert VM after a reviewer-path failure.

  Run on the Hyper-V host after the app has failed while signed in as CertUser:

    cd H:\PRONTOPC_PRODUCTION\tests\CertVM
    .\collect-certuser-evidence.ps1

  Output is copied to H:\PRONTOPC_PRODUCTION\tests\CertVM\evidence\<timestamp>.
#>
param(
    [string]$VmName = 'ProntoPC-CertVM',
    [string]$UserName = 'CertUser',
    [string]$OutDir = (Join-Path $PSScriptRoot ('evidence\{0:yyyyMMdd-HHmmss}' -f (Get-Date)))
)
$ErrorActionPreference = 'Stop'

function Step($text) { Write-Host "`n$text" -ForegroundColor Cyan }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Step "Sign in with the VM ADMIN account"
$cred = Get-Credential -Message "Admin account INSIDE $VmName"

Step "Collecting CertUser ProntoPC logs, crash events, package state, and profile facts"
$session = New-PSSession -VMName $VmName -Credential $cred
try {
    $remoteRoot = 'C:\CertTest\evidence'
    Invoke-Command -Session $session -ScriptBlock {
        param($remoteRoot, $UserName)
        $ErrorActionPreference = 'Stop'
        New-Item -ItemType Directory -Force -Path $remoteRoot | Out-Null

        $localUser = Get-LocalUser -Name $UserName -ErrorAction SilentlyContinue
        $sid = if ($localUser) { $localUser.SID.Value } else { $null }
        $profile = Join-Path 'C:\Users' $UserName
        if ($sid) {
            try {
                $profileKey = Get-ItemProperty -Path ("HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + $sid) -ErrorAction Stop
                if ($profileKey.ProfileImagePath) { $profile = [Environment]::ExpandEnvironmentVariables($profileKey.ProfileImagePath) }
            } catch { }
        }
        $facts = [ordered]@{
            CollectedUtc = (Get-Date).ToUniversalTime().ToString('o')
            UserName = $UserName
            UserExists = [bool]$localUser
            UserSid = $sid
            ProfilePath = $profile
            ProfileExists = Test-Path -LiteralPath $profile
            OSVersion = [Environment]::OSVersion.Version.ToString()
            UAC = (Get-ItemProperty HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System).EnableLUA
            CertUserIsAdmin = [bool](Get-LocalGroupMember Administrators -ErrorAction SilentlyContinue | Where-Object Name -like "*\$UserName")
        }
        $facts | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $remoteRoot 'facts.json') -Encoding UTF8

        Get-AppxPackage -AllUsers *ProntoPC* |
            Select-Object Name, PackageFullName, PackageFamilyName, Version, InstallLocation, SignatureKind, Status |
            ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $remoteRoot 'appx-prontopc.json') -Encoding UTF8

        if (Test-Path -LiteralPath $profile) {
            Get-ChildItem -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -in @('plan-authoring.log','prontopc-startup.log') -or $_.FullName -like '*ProntoPC*' } |
                Select-Object FullName, Length, LastWriteTimeUtc |
                ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $remoteRoot 'certuser-prontopc-files.json') -Encoding UTF8

            Get-ChildItem -LiteralPath $profile -Recurse -Force -Filter 'plan-authoring.log' -ErrorAction SilentlyContinue |
                ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $remoteRoot ('plan-authoring-' + ($_.FullName -replace '[:\\/ ]','_') + '.log')) -Force
                }

            Get-ChildItem -LiteralPath $profile -Recurse -Force -Filter 'prontopc-startup.log' -ErrorAction SilentlyContinue |
                ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $remoteRoot ('startup-' + ($_.FullName -replace '[:\\/ ]','_') + '.log')) -Force
                }
        }

        Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddHours(-6)} -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -match 'Application Error|\.NET Runtime|Windows Error Reporting' -or $_.Message -match 'ProntoPC|onnx|0xc0000142' } |
            Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message |
            ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $remoteRoot 'application-events.json') -Encoding UTF8

        Get-ChildItem -LiteralPath $remoteRoot -Force | Select-Object FullName, Length, LastWriteTimeUtc
    } -ArgumentList $remoteRoot, $UserName | Tee-Object -FilePath (Join-Path $OutDir 'remote-manifest.txt')

    Copy-Item -FromSession $session -Path 'C:\CertTest\evidence\*' -Destination $OutDir -Recurse -Force
}
finally {
    if ($session) { Remove-PSSession $session }
}

Write-Host "`nEvidence copied to: $OutDir" -ForegroundColor Green
Get-ChildItem -LiteralPath $OutDir -Force | Select-Object Name, Length, LastWriteTimeUtc | Format-Table -AutoSize