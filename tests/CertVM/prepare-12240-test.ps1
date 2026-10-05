#Requires -RunAsAdministrator
param(
    [string]$VmName = 'ProntoPC-CertVM',
    # Exact certificate exported for the previous working VM test package.
    [string]$SigningCertificateThumbprint = 'CCC70E625DB54A1B2CFA523B91C90C21D7D94857'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$source = Join-Path $repo 'StoreApp\ProntoPC.msix'
$expectedSource = 'F150F2982E82896F953579EDF889F73DD2E901FAF353F30E0D60DC3B2D0A2048'
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expectedSource) {
    throw 'Prepared 1.2.24.0 source hash mismatch. Stop; do not stage another package.'
}
$vm = Get-VM -Name $VmName
if ($vm.State -ne 'Running') { throw 'Start the existing Cert VM before running this update script.' }
$guestService = Get-VMIntegrationService -VMName $VmName -Name 'Guest Service Interface'
if (-not $guestService.Enabled) { throw 'Guest Service Interface is disabled. This script will not enable it automatically.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($source)
try {
    $entry = $zip.GetEntry('AppxManifest.xml')
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
} finally { $zip.Dispose() }
if ($manifest.Package.Identity.Version -ne '1.2.24.0') { throw 'Package manifest version mismatch.' }
$publisher = $manifest.Package.Identity.Publisher
$certs = @(Get-ChildItem Cert:\LocalMachine\My | Where-Object {
    $_.Thumbprint -eq $SigningCertificateThumbprint -and $_.Subject -eq $publisher -and
    $_.HasPrivateKey -and $_.NotBefore -le (Get-Date) -and $_.NotAfter -gt (Get-Date)
})
if ($certs.Count -ne 1) {
    throw "Existing signing certificate $SigningCertificateThumbprint is missing, expired, not yet valid, or does not match the publisher/private-key requirement. No certificate or trust will be created."
}
Write-Host "Using existing test certificate: $($certs[0].Thumbprint)"
$signTool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe' | Sort-Object FullName | Select-Object -Last 1
if (-not $signTool) { throw 'Windows SDK signtool.exe not found.' }
$stage = Join-Path 'C:\ProntoPC-Sandbox' ('update-12240-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $stage | Out-Null
$copy = Join-Path $stage 'ProntoPC-test.msix'
Copy-Item -LiteralPath $source -Destination $copy
& $signTool.FullName sign /fd SHA256 /sm /sha1 $certs[0].Thumbprint $copy
if ($LASTEXITCODE -ne 0) { throw "Test-copy signing failed: $LASTEXITCODE" }
Set-Content -LiteralPath (Join-Path $stage 'source-sha256.txt') -Value $expectedSource
Set-Content -LiteralPath (Join-Path $stage 'signed-sha256.txt') -Value (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $stage 'test-cert-thumbprint.txt') -Value $certs[0].Thumbprint
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-12240-current-user.ps1') -Destination $stage
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expectedSource) { throw 'Unsigned source changed unexpectedly.' }
$guestDir = 'C:\CertTest\ProntoPC-1.2.24.0'
foreach ($name in @('ProntoPC-test.msix','source-sha256.txt','signed-sha256.txt','test-cert-thumbprint.txt','install-12240-current-user.ps1')) {
    Copy-VMFile -Name $VmName -SourcePath (Join-Path $stage $name) -DestinationPath (Join-Path $guestDir $name) -FileSource Host -CreateFullPath -Force
}
Write-Host 'Prepared 1.2.24.0 test copy; unsigned WACK/Store package unchanged.' -ForegroundColor Green
Write-Host 'Inside the VM, signed in as PreFlight, close ProntoPC and run:'
Write-Host 'powershell -NoProfile -ExecutionPolicy Bypass -File C:\CertTest\ProntoPC-1.2.24.0\install-12240-current-user.ps1'
Write-Host 'No VM checkpoint was restored and no certificate trust was changed.'


