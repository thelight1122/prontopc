$ErrorActionPreference = 'Stop'
Start-Transcript -Path H:\PRONTOPC_PRODUCTION\docs\CERT_VM_12260_PREPARATION_LOG.txt -Force
try {
    & H:\PRONTOPC_PRODUCTION\tests\CertVM\prepare-12260-test.ps1
} finally { Stop-Transcript }
