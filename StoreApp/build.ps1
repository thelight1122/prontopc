# ProntoPC — reproducible MSIX build.
#
#   .\build.ps1              # build package-layout + ProntoPC.msix + ProntoPC.msixupload (UNSIGNED)
#   .\build.ps1 -Sign        # also sign with the local self-signed test cert (sideload only)
#
# The UNSIGNED ProntoPC.msix is what goes to Partner Center — Microsoft signs it.
# Only use -Sign for local sideload testing via reinstall.ps1.

param([switch]$Sign)

$ErrorActionPreference = 'Stop'
$dir    = $PSScriptRoot
$layout = Join-Path $dir 'package-layout'
$msix   = Join-Path $dir 'ProntoPC.msix'
$upload = Join-Path $dir 'ProntoPC.msixupload'
$kit    = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\10.0.26100.0\x64'

function Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }

# 1 — publish self-contained win-x64
Step 'Cleaning stale WPF build intermediates'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
foreach ($stale in @('bin', 'obj')) {
    $stalePath = Join-Path $dir $stale
    if (Test-Path $stalePath) { Remove-Item $stalePath -Recurse -Force }
}

Step 'dotnet publish (Release, win-x64, self-contained)'
dotnet publish (Join-Path $dir 'ProntoPC.csproj') -c Release -r win-x64 --self-contained true -o $layout
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# 2 — strip symbols: .pdb files must not ship in the Store package
Step 'Removing .pdb symbols from the layout'
Get-ChildItem $layout -Filter *.pdb -Recurse | Remove-Item -Force

# 3 — manifest + assets (Package.appxmanifest is the single source of truth)
Step 'Staging AppxManifest.xml and Assets'
$stagedManifest = Join-Path $layout 'AppxManifest.xml'
Copy-Item (Join-Path $dir 'Package.appxmanifest') $stagedManifest -Force
# The shared manifest carries the .wapproj build-time tokens ($targetnametoken$ /
# $targetentrypoint$) that Visual Studio substitutes when packaging via the WAP
# project. makeappx does NOT substitute them, so resolve them here in the STAGED
# copy only (the source Package.appxmanifest is left untouched, so both the
# makeappx path and the .wapproj path keep working from one manifest).
(Get-Content $stagedManifest -Raw).
    Replace('$targetnametoken$.exe', 'ProntoPC.exe').
    Replace('$targetentrypoint$', 'Windows.FullTrustApplication') |
    Set-Content $stagedManifest -Encoding UTF8
Copy-Item (Join-Path $dir 'Assets') $layout -Recurse -Force
# Warm-up clips are retained in source but excluded from the shipped product.
$warmupClips = Join-Path $layout 'Assets\warmup'
if (Test-Path -LiteralPath $warmupClips) {
    if ([IO.Path]::GetFullPath($warmupClips) -ne [IO.Path]::GetFullPath((Join-Path $dir 'package-layout\Assets\warmup'))) { throw 'Unexpected warm-up staging path' }
    Remove-Item -LiteralPath $warmupClips -Recurse -Force
}

# ONNX Runtime GenAI imports the desktop VC++ runtime. Store reviewer VMs cannot be
# assumed to have those DLLs globally, and a launch-time native load failure can
# surface only as ProntoPC.exe 0xc0000142 before managed crash logging runs. Ship the
# x64 runtime beside the executable so the package is self-contained on clean devices.
Step 'Staging VC++ runtime DLLs for ONNX Runtime'
$vcRedistRoot = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\BuildTools\VC\Redist\MSVC'
$vcRuntimeNames = @('MSVCP140.dll', 'MSVCP140_1.dll', 'VCRUNTIME140.dll', 'VCRUNTIME140_1.dll')
$vcRuntimeDir = Get-ChildItem $vcRedistRoot -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64\Microsoft.VC143.CRT' } |
    Where-Object {
        $candidate = $_
        (Test-Path $candidate) -and (($vcRuntimeNames | Where-Object { -not (Test-Path (Join-Path $candidate $_)) }).Count -eq 0)
    } |
    Select-Object -First 1
if (-not $vcRuntimeDir) {
    throw "Could not locate x64 Microsoft.VC143.CRT redist DLLs under $vcRedistRoot"
}
foreach ($name in $vcRuntimeNames) {
    Copy-Item (Join-Path $vcRuntimeDir $name) (Join-Path $layout $name) -Force
}

# 3b — prune non-shipped models. Only the Canon-formed responder ships; the IRG's
# analyzer research model (gemma-3-1b-analyzer) lives under Assets\models for the on-device smoke
# but is NOT part of the shipped product (governance gate enabled:false) and must not be packaged.
$modelsDir = Join-Path $layout 'Assets\models'
if (Test-Path $modelsDir) {
    Get-ChildItem $modelsDir -Directory |
        Where-Object { $_.Name -ne 'gemma-3-1b-it-formed-v0.2.1' } |
        ForEach-Object { Step "Pruning non-shipped model: $($_.Name)"; Remove-Item $_.FullName -Recurse -Force }
}

# 4 — pack
Step 'makeappx pack'
if (Test-Path $msix) { Remove-Item $msix -Force }
if (Test-Path $upload) { Remove-Item $upload -Force }
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

# 6 — Store upload container. Partner Center accepts raw .msix in some paths, but
# Microsoft recommends the .msixupload container for Windows 10/11 Store submissions.
if (-not $Sign) {
    Step 'Creating Store upload container (.msixupload)'
    $uploadStage = Join-Path $dir 'upload-stage'
    if (Test-Path $uploadStage) { Remove-Item $uploadStage -Recurse -Force }
    New-Item -ItemType Directory -Path $uploadStage | Out-Null
    $uploadPackageName = "ProntoPC_${ver}_x64.msix"
    Copy-Item $msix (Join-Path $uploadStage $uploadPackageName) -Force
    # Windows PowerShell Compress-Archive rejects the required .msixupload suffix.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($uploadStage, $upload, [IO.Compression.CompressionLevel]::Optimal, $false)
    Remove-Item $uploadStage -Recurse -Force
}

$size = [math]::Round((Get-Item $msix).Length / 1MB, 1)
Write-Host ""
Write-Host "Built ProntoPC.msix  version $ver  ${size} MB  signed=$($Sign.IsPresent)" -ForegroundColor Green
if (-not $Sign) {
    $uploadSize = [math]::Round((Get-Item $upload).Length / 1MB, 1)
    Write-Host "Built ProntoPC.msixupload  version $ver  ${uploadSize} MB" -ForegroundColor Green
    Write-Host "UNSIGNED - upload ProntoPC.msixupload to Partner Center; keep ProntoPC.msix for WACK/local inspection." -ForegroundColor Yellow
}
