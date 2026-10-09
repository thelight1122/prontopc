# Authorized diagnostic run — October 3, 2026

Owner explicitly authorized signing a separate 1.2.22.0 diagnostic copy, transferring
it to existing ProntoPC-CertVM, and updating PreFlight to obtain the actual model
loader exception. No Partner Center submission is authorized.

Agent attempt: unsigned diagnostic hash verified as
471BACCE2A94750092EC66C254DBC31126D4E7B5BD33CB0D73D92FBC5590859B;
host preparation stopped at #Requires -RunAsAdministrator, ScriptRequiresElevation.
The script body did not execute. No signing, transfer or install observed.

1. HOST DMTAI-DEV: Start menu > Windows PowerShell > Run as administrator.
   Keep the existing ProntoPC-CertVM running. Execute:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "H:\PRONTOPC_PRODUCTION\tests\CertVM\prepare-12220-diagnostic.ps1"
```

Expected: Prepared 1.2.22.0 test copy; unsigned WACK/Store package unchanged.
This signs a separate copy using existing certificate
CCC70E625DB54A1B2CFA523B91C90C21D7D94857 and transfers package, bindings and guest
installer to C:\CertTest\ProntoPC-1.2.22.0. No certificate trust is created or VM
checkpoint reset. If it errors, stop and provide the output; do not alter trust.

2. INSIDE VM: sign in as PreFlight. Close ProntoPC. In ordinary PowerShell run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\CertTest\ProntoPC-1.2.22.0\install-12220-current-user.ps1"
```

Expected: UPDATE VERIFIED: ProntoPC 1.2.22.0 for the current account. Stop if an
error occurs. This updates the current account, verifies existing certificate
trust, file hashes, package and DLL version; it does not remove the previous
package/data or reset the VM. Do not run the guest command on the host.

3. INSIDE VM as PreFlight: Start > ProntoPC. Run one scan and wait for the plan
or retry result. The diagnostic retains baseline UI without the reporting footer.
Note the run time and visible outcome. Then run:

```powershell
$diagPackage = Get-AppxPackage -Name '5DServiceSolutionsGlobal.ProntoPC'
$diagPackage | Format-List Name,Version,PackageFullName,InstallLocation
$diagLogPaths = @(
    "$env:LOCALAPPDATA\ProntoPC\logs\model-load.log"
    "$env:LOCALAPPDATA\Packages\$($diagPackage.PackageFamilyName)\LocalCache\Local\ProntoPC\logs\model-load.log"
)
foreach ($path in $diagLogPaths) {
    Write-Output "LOG: $path"
    if (Test-Path -LiteralPath $path) {
        Get-Item -LiteralPath $path | Format-List FullName,LastWriteTimeUtc,Length
        Get-Content -LiteralPath $path -Tail 100
    } else { Write-Output 'Not found' }
}
```

Provide host preparation result, guest update result, visible app outcome and log
output. Redact personal details if any appear. A missing log is a shortfall, not
evidence of successful loading. Record only actual observations. Diagnostic is
not a Store release or certification-ready claim. Original archived 1.2.21 upload
hash remains 8D2FF6B1867FE22BE1612A239B906F80E931EBD9BBB6ED48993DC1B36CFD82C3;
this diagnostic cannot satisfy its installed reporting checks.
