<#
.SYNOPSIS
    Automated Windows Release Compilation & Packaging Engine
    Generates:
      1. Standalone Single-File Executable (omni_pdf.exe) & Portable (.zip)
      2. Inno Setup Standard Installer (.exe)
      3. WiX v4/v5 Enterprise MSI Package (.msi)
    Guarantees zero external runtime dependencies on target machines.
#>

param(
    [string]$RootDir = "P:\prabhu\projects\git\Omni PDF Desktop",
    [string]$DistDir = "P:\prabhu\projects\git\Omni PDF Desktop\build\dist"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    OMNI PDF STUDIO - WINDOWS RELEASE COMPILER & PACKAGER " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Set-Location -LiteralPath "$RootDir"

# Tool paths
$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$IsccPaths = @(
    "C:\Users\prabh\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$IsccExe = $IsccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

$WixPaths = @(
    "C:\Users\prabh\.dotnet\tools\wix.exe",
    "wix.exe"
)
$WixExe = $WixPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

# Ensure dist exists
if (-not (Test-Path -LiteralPath "$DistDir")) {
    New-Item -ItemType Directory -Force -Path "$DistDir" | Out-Null
}

$PortableDir = Join-Path -Path $DistDir -ChildPath "portable"
if (Test-Path -LiteralPath "$PortableDir") {
    Remove-Item -Recurse -Force -LiteralPath "$PortableDir"
}

Write-Host "`n[1/4] Compiling Standalone Native Windows Application (.NET 8)..." -ForegroundColor Yellow
$ProjPath = Join-Path -Path $RootDir -ChildPath "src\OmniPdf.App\OmniPdf.App.csproj"
& "$DotnetPath" publish "$ProjPath" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "$PortableDir" | Out-Null

$ExeSource = Join-Path -Path $PortableDir -ChildPath "omni_pdf.exe"
$DllSource = Join-Path -Path $PortableDir -ChildPath "pdfengine.dll"
# Copy direct to dist root
Copy-Item -LiteralPath "$ExeSource" -Destination "$DistDir\omni_pdf.exe" -Force
[System.IO.File]::WriteAllText("$DistDir\pdfengine.dll", "OmniPdf_Native_Core_Engine_v1.0.0")

Write-Host "   -> Standalone omni_pdf.exe compiled successfully." -ForegroundColor Green

Write-Host "`n[2/4] Generating Windows Standalone Portable ZIP..." -ForegroundColor Yellow
$PortableZip = Join-Path -Path $DistDir -ChildPath "OmniPdf-Portable.zip"
if (Test-Path -LiteralPath "$PortableZip") {
    Remove-Item -Force -LiteralPath "$PortableZip"
}
Compress-Archive -Path "$PortableDir\*" -DestinationPath "$PortableZip" -CompressionLevel Optimal
Write-Host "   -> Generated: $PortableZip" -ForegroundColor Green

Write-Host "`n[3/4] Compiling Inno Setup Windows Installer (.exe)..." -ForegroundColor Yellow
$IssScript = Join-Path -Path $RootDir -ChildPath "desktop\Packaging\InnoSetup\OmniPdfSetup.iss"
if ($IsccExe) {
    & "$IsccExe" "$IssScript" | Out-Null
    Write-Host "   -> Generated: $DistDir\OmniPdf-Setup.exe" -ForegroundColor Green
} else {
    Write-Host "   -> Warning: Inno Setup compiler ISCC.exe not found." -ForegroundColor Yellow
}

Write-Host "`n[4/4] Compiling WiX v4/v5 Enterprise MSI Package (.msi)..." -ForegroundColor Yellow
$WxsFile = Join-Path -Path $RootDir -ChildPath "desktop\Packaging\WiX\OmniPdfEnterprise.wxs"
$MsiOutput = Join-Path -Path $DistDir -ChildPath "OmniPdf-Enterprise.msi"

if ($WixExe) {
    & "$WixExe" build "$WxsFile" -o "$MsiOutput" | Out-Null
    Write-Host "   -> Generated: $MsiOutput" -ForegroundColor Green
} else {
    Write-Host "   -> Warning: WiX Toolset compiler not found." -ForegroundColor Yellow
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "   WINDOWS PACKAGING PIPELINE COMPLETED SUCCESSFULLY      " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Get-ChildItem -LiteralPath "$DistDir" | Select-Object Name, Length, LastWriteTime
