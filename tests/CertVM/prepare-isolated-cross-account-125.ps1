#Requires -RunAsAdministrator
# Clone an existing checkpoint; never restore or alter the source VM.
$ErrorActionPreference = 'Stop'
$repo = 'H:\PRONTOPC_PRODUCTION'
$cloneName = 'ProntoPC-UAC-125-20261008'
$root = 'G:\AEGIS_RECOVERY_BACKUPS\ProntoPC-20261009T005215Z\cross-account-125-vm'
$resultPath = Join-Path $repo 'tmp\isolated-uac125-preparation.json'
$phase = 'validate'
$clone = $null
function Record-Phase([string]$Name) {
    $script:phase = $Name
    [ordered]@{utc=[DateTime]::UtcNow.ToString('O');status='running';phase=$Name;clone_name=$cloneName;root=$root} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resultPath -Encoding UTF8
}
try {
    if (Get-VM -Name $cloneName -ErrorAction SilentlyContinue) { throw 'Diagnostic VM name already exists; stop rather than overwrite.' }
    if (Test-Path -LiteralPath $root) { throw 'Diagnostic export directory already exists; stop rather than overwrite.' }
    $source = Get-VM -Name 'ProntoPC-CertVM'
    $checkpoint = Get-VMCheckpoint -VM $source -Name 'Cert_User_added'
    $package = 'C:\ProntoPC-Sandbox\update-12250-20261005-055400-067\ProntoPC-test.msix'
    $expected = '22C455096294D59F88CF5DE3198748DE3AD5652E7E5FC1791378BEA92C9BAB77'
    if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expected) { throw 'Historical 1.2.25 test-copy hash mismatch.' }
    New-Item -ItemType Directory -Path $root | Out-Null
    $export = Join-Path $root 'checkpoint-export'
    Record-Phase 'export-existing-checkpoint'
    Export-VMSnapshot -VMSnapshot $checkpoint -Path $export
    $configs = @(Get-ChildItem -LiteralPath $export -Recurse -File -Filter '*.vmcx' | Where-Object FullName -NotMatch '\\Snapshots\\')
    if ($configs.Count -ne 1) { throw "Expected one exported VM configuration; found $($configs.Count)." }
    $runtime = Join-Path $root 'diagnostic-runtime'
    foreach ($dir in @($runtime, (Join-Path $runtime 'disks'), (Join-Path $runtime 'snapshots'), (Join-Path $runtime 'paging'))) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    Record-Phase 'import-independent-copy'
    $clone = Import-VM -Path $configs[0].FullName -Copy -GenerateNewId -VirtualMachinePath $runtime -VhdDestinationPath (Join-Path $runtime 'disks') -SnapshotFilePath (Join-Path $runtime 'snapshots') -SmartPagingFilePath (Join-Path $runtime 'paging')
    Rename-VM -VM $clone -NewName $cloneName
    Get-VMNetworkAdapter -VM $clone | Disconnect-VMNetworkAdapter
    Enable-VMIntegrationService -VM $clone -Name 'Guest Service Interface'
    Record-Phase 'start-isolated-copy'
    Start-VM -VM $clone
    $guest = 'C:\CertTest\CrossAccount-125'
    $deadline = [DateTime]::UtcNow.AddMinutes(4)
    do {
        try {
            Copy-VMFile -VM $clone -SourcePath $package -DestinationPath (Join-Path $guest 'ProntoPC-test.msix') -FileSource Host -CreateFullPath -ErrorAction Stop
            $copied = $true
        } catch {
            $copied = $false
            if ([DateTime]::UtcNow -ge $deadline) { throw }
            Start-Sleep -Seconds 5
        }
    } until ($copied)
    foreach ($name in @('run-cross-account-125.ps1')) {
        Copy-VMFile -VM $clone -SourcePath (Join-Path $repo ('tests\CertVM\' + $name)) -DestinationPath (Join-Path $guest $name) -FileSource Host -CreateFullPath
    }
    $result = [ordered]@{
        utc=[DateTime]::UtcNow.ToString('O');status='prepared';clone_name=$cloneName;clone_id=$clone.Id.ToString()
        source_vm=$source.Name;source_vm_id=$source.Id.ToString();source_checkpoint=$checkpoint.Name;source_checkpoint_id=$checkpoint.Id.ToString()
        source_checkpoint_restored=$false;source_vm_mutated=$false;network='disconnected on diagnostic copy'
        root=$root;guest_directory=$guest;signed_test_package_sha256=$expected
        signing_performed=$false;trust_changed=$false;guest_install_observed=$false;uac_reproduction='not yet observed'
    }
} catch {
    $result = [ordered]@{utc=[DateTime]::UtcNow.ToString('O');status='shortfall';phase=$phase;error=$_.Exception.Message;position=$_.InvocationInfo.PositionMessage;root=$root;source_vm_mutated=$false}
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding UTF8
