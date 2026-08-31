# ProntoPC — reproducible MSIX build.
#
#   .\build.ps1              # build package-layout + ProntoPC.msix (UNSIGNED)
#   .\build.ps1 -Sign        # also sign with the local self-signed test cert (sideload only)
#
# The UNSIGNED ProntoPC.msix is what goes to Partner Center — Microsoft signs it.
# Only use -Sign for local sideload testing via reinstall.ps1.

param([switch]$Sign)

$ErrorActionPreference = 'Stop'
$dir    = $PSScriptRoot
$layout = Join-Path $dir 'package-layout'
$msix   = Join-Path $dir 'ProntoPC.msix'
$kit    = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\10.0.26100.0\x64'

function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }

# 1 — publish self-contained win-x64
Step 'dotnet publish (Release, win-x64, self-contained)'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
dotnet publish (Join-Path $dir 'ProntoPC.csproj') -c Release -r win-x64 --self-contained true -o $layout
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# 2 — strip symbols: .pdb files must not ship in the Store package
Step 'Removing .pdb symbols from the layout'
Get-ChildItem $layout -Filter *.pdb -Recurse | Remove-Item -Force

# 3 — manifest + assets (Package.appxmanifest is the single source of truth)
Step 'Staging AppxManifest.xml and Assets'
Copy-Item (Join-Path $dir 'Package.appxmanifest') (Join-Path $layout 'AppxManifest.xml') -Force
Copy-Item (Join-Path $dir 'Assets') $layout -Recurse -Force

# 4 — pack
Step 'makeappx pack'
if (Test-Path $msix) { Remove-Item $msix -Force }
& (Join-Path $kit 'makeappx.exe') pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }

[xml]$m = Get-Content (Join-Path $dir 'Package.appxmanifest')
$ver = $m.Package.Identity.Version

# 5 — optional local signing (sideload only — NEVER for Store submission)
if ($Sign) {
    Step 'Signing with local test cert (sideload only)'
    $cert = Get-ChildItem Cert:\LocalMachine\My |
            Where-Object { $_.Subject -eq $m.Package.Identity.Publisher } | Select-Object -First 1
    if (-not $cert) { throw "No signing cert matching $($m.Package.Identity.Publisher) in LocalMachine\My" }
    & (Join-Path $kit 'signtool.exe') sign /fd SHA256 /sm /sha1 $cert.Thumbprint $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }
}

$size = [math]::Round((Get-Item $msix).Length / 1MB, 1)
Write-Host ""
Write-Host "Built ProntoPC.msix  version $ver  ${size} MB  signed=$($Sign.IsPresent)" -ForegroundColor Green
if (-not $Sign) { Write-Host "UNSIGNED — this is the file to upload to Partner Center." -ForegroundColor Yellow }
