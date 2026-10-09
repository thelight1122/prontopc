# Run INSIDE the isolated diagnostic VM as CertUser, in ordinary PowerShell.
# Prepare verifies and installs the preserved test copy for this account only.
# Capture records evidence after the owner tests the real Perform Repairs UI.
param(
    [ValidateSet('Prepare','Capture')][string]$Mode = 'Prepare',
    [string]$AdminAccountUsed = '',
    [ValidateSet('Unobserved','Native0142','RepairsCompleted','UacCancelled','Other')]
    [string]$ObservedOutcome = 'Unobserved'
)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$adminSid = 'S-1-5-32-544'
if ($env:USERNAME -ne 'CertUser') { throw 'Sign in as CertUser INSIDE ProntoPC-UAC-125-20261008. Do not run this on the host or as PreFlight.' }
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -or $identity.Groups.Value -contains $adminSid) {
    throw 'This must be a standard account, not an administrator with a filtered token.'
}
$uac = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
if ($uac.EnableLUA -ne 1 -or $uac.ConsentPromptBehaviorUser -notin @(1,3)) {
    throw 'This test requires UAC enabled with a credential prompt for standard users. No policy was changed.'
}
$packageName = '5DServiceSolutionsGlobal.ProntoPC'
$version = '1.2.25.0'
$expectedSigned = '22C455096294D59F88CF5DE3198748DE3AD5652E7E5FC1791378BEA92C9BAB77'
$expectedDll = '8901AADA46CB1757558F107085A2B95092625E0BF04CED37BAF3C1998AB433FB'
$runRoot = Join-Path $env:LOCALAPPDATA 'ProntoPC-CrossAccount-Tests'
if ($Mode -eq 'Prepare') {
    $package = Join-Path $PSScriptRoot 'ProntoPC-test.msix'
    if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedSigned) { throw 'Preserved signed 1.2.25 test-copy hash mismatch.' }
    $thumbprint = 'CCC70E625DB54A1B2CFA523B91C90C21D7D94857'
    $trusted = @(Get-ChildItem Cert:\LocalMachine\TrustedPeople; Get-ChildItem Cert:\CurrentUser\TrustedPeople) |
        Where-Object Thumbprint -eq $thumbprint
    if (-not $trusted) { throw 'Historical test certificate is not already trusted in this snapshot. Stop; do not import certificates or change trust.' }
    if (Get-Process -Name ProntoPC -ErrorAction SilentlyContinue) { throw 'Close ProntoPC before preparing this test.' }
    $installed = Get-AppxPackage -Name $packageName
    if ($installed -and [version]$installed.Version -gt [version]$version) {
        throw 'A newer package is registered for CertUser. No downgrade/removal was attempted.'
    }
    $dependency = @(Get-AppxPackage -Name 'Microsoft.VCLibs.140.00.UWPDesktop' | Where-Object { [version]$_.Version -ge [version]'14.0.33728.0' })
    if (-not $dependency) { throw 'The required VCLibs dependency is not registered for CertUser. Stop and retain this result; no dependency or trust was changed.' }
    if (-not $installed -or [version]$installed.Version -lt [version]$version) {
        Add-AppxPackage -Path $package -ErrorAction Stop
    }
    $installed = Get-AppxPackage -Name $packageName
    if (-not $installed -or $installed.Version -ne $version) { throw 'Exact 1.2.25.0 is not installed for CertUser.' }
    if ((Get-FileHash -LiteralPath (Join-Path $installed.InstallLocation 'ProntoPC.dll')).Hash -ne $expectedDll) {
        throw 'Installed DLL differs from the preserved 1.2.25 payload. Do not continue.'
    }
    $run = Join-Path $runRoot ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $run | Out-Null
    $os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $facts = [ordered]@{
        started_utc=[DateTime]::UtcNow.ToString('O');user=$identity.Name;sid=$identity.User.Value;standard_account=$true
        os_build=$os.CurrentBuild;os_ubr=$os.UBR;os_display_version=$os.DisplayVersion
        package_version=$installed.Version.ToString();package_full_name=$installed.PackageFullName
        package_location=$installed.InstallLocation;signed_test_sha256=$expectedSigned;installed_dll_sha256=$expectedDll
        rejected_upload_sha256='0B437B154E39401E872ED7BD019C77573536D2EA1388228EE46F06B67FCBA1E8'
        uac=$uac | Select-Object EnableLUA,ConsentPromptBehaviorUser,PromptOnSecureDesktop
        status='prepared only; Perform Repairs not yet observed'
    }
    $facts | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'start.json') -Encoding UTF8
    Write-Host "EXACT 1.2.25 PREPARED for $($identity.Name). Evidence: $run" -ForegroundColor Green
    Write-Host 'Start ProntoPC -> Scan -> Findings -> Perform Repairs. At UAC use PreFlight, a DIFFERENT existing administrator.'
    Write-Host 'Note whether any error appears at initial launch or only after Perform Repairs. Photograph/screenshot 0xc0000142 if present.'
    Write-Host 'Afterward run this script with -Mode Capture -AdminAccountUsed PreFlight -ObservedOutcome <actual outcome>.'
    Start-Process explorer.exe -ArgumentList ('shell:AppsFolder\' + $installed.PackageFamilyName + '!ProntoPC')
    return
}
$runs = @(Get-ChildItem -LiteralPath $runRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
if (-not $runs) { throw 'No prepared run exists for this account. Run Prepare first.' }
$run = $runs[0].FullName
$start = Get-Content -LiteralPath (Join-Path $run 'start.json') -Raw | ConvertFrom-Json
$capture = Join-Path $run ('capture-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $capture | Out-Null
$events = @(Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=[datetime]$start.started_utc} -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match 'ProntoPC|0xc0000142' } |
    Select-Object TimeCreated,ProviderName,Id,LevelDisplayName,Message)
$current = Get-AppxPackage -Name $packageName
if (-not $current) { throw 'ProntoPC is no longer registered for the initiating account. Retain start.json and report this shortfall.' }
$dirs = @(
    (Join-Path $env:LOCALAPPDATA 'ProntoPC'),
    (Join-Path $env:LOCALAPPDATA ('Packages\' + $current.PackageFamilyName + '\LocalCache\Local\ProntoPC'))
)
$requestFiles = @()
foreach ($dir in $dirs) {
    $logs = Join-Path $dir 'logs'
    foreach ($name in @('startup-crash.log','plan-authoring.log','model-load.log')) {
        $path = Join-Path $logs $name
        if (Test-Path -LiteralPath $path) {
            $prefix = if ($dir -like '*\Packages\*') { 'redirected-' } else { 'ordinary-' }
            Copy-Item -LiteralPath $path -Destination (Join-Path $capture ($prefix + $name))
        }
    }
    $requestFiles += @(Get-ChildItem -LiteralPath (Join-Path $dir 'repair-requests') -File -ErrorAction SilentlyContinue |
        Select-Object FullName,Length,CreationTimeUtc,LastWriteTimeUtc)
}
$reportedAdmin = $null
if ($AdminAccountUsed) {
    $leaf = $AdminAccountUsed.Split('\')[-1]
    $reportedAdmin = Get-CimInstance Win32_UserAccount -Filter 'LocalAccount=True' -ErrorAction SilentlyContinue |
        Where-Object Name -EQ $leaf | Select-Object -First 1 Name,SID
}
$outcome = [ordered]@{
    captured_utc=[DateTime]::UtcNow.ToString('O');run_start=$start;current_user=$identity.Name
    admin_account_reported_by_operator=$AdminAccountUsed;outcome_reported_by_operator=$ObservedOutcome
    reported_admin_local_account=$reportedAdmin
    reported_admin_resolves_to_different_local_sid=($null -ne $reportedAdmin -and $reportedAdmin.SID -ne $identity.User.Value)
    observation_provenance='Outcome/admin fields are operator declarations, not an independently observed UI run.'
    current_package_version=$current.Version.ToString();application_events=$events;request_file_metadata=$requestFiles
    absent_events_do_not_prove_success=$true;hypothesis='unconfirmed until account, package and visible failure evidence agree'
}
$file = Join-Path $capture 'outcome.json'
$outcome | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $file -Encoding UTF8
Write-Host "Evidence saved: $file" -ForegroundColor Green
