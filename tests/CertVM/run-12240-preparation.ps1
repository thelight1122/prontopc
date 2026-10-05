$ErrorActionPreference = 'Stop'
Start-Transcript -Path H:\PRONTOPC_PRODUCTION\docs\CERT_VM_12240_PREPARATION_LOG.txt -Force
try {
    & H:\PRONTOPC_PRODUCTION\tests\CertVM\prepare-12240-test.ps1
} finally { Stop-Transcript }
