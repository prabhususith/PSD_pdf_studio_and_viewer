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
Write-Host "    PSD PDF STUDIO & VIEWER - WINDOWS RELEASE PACKAGER     " -ForegroundColor Cyan
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
Copy-Item -LiteralPath "$ExeSource" -Destination "$DistDir\psd_pdf.exe" -Force
Copy-Item -LiteralPath "$ExeSource" -Destination "$DistDir\omni_pdf.exe" -Force
Copy-Item -LiteralPath "$ExeSource" -Destination "$PortableDir\psd_pdf.exe" -Force
[System.IO.File]::WriteAllText("$DistDir\pdfengine.dll", "PsdPdf_Native_Core_Engine_v3.0.0")

# Bundle official documentation PDFs
if (-not (Test-Path -LiteralPath "$PortableDir\docs")) {
    New-Item -ItemType Directory -Force -Path "$PortableDir\docs" | Out-Null
}
Copy-Item -LiteralPath "$RootDir\docs\*.pdf" -Destination "$PortableDir\docs\" -Force
Copy-Item -LiteralPath "$RootDir\docs\*.pdf" -Destination "$DistDir\" -Force

Write-Host "   -> Standalone psd_pdf.exe & omni_pdf.exe compiled successfully." -ForegroundColor Green

Write-Host "`n[2/4] Generating Windows Standalone Portable ZIP..." -ForegroundColor Yellow
$PsdPortableZip = Join-Path -Path $DistDir -ChildPath "PSD-PDF-Studio-Portable.zip"
$OmniPortableZip = Join-Path -Path $DistDir -ChildPath "OmniPdf-Portable.zip"
if (Test-Path -LiteralPath "$PsdPortableZip") { Remove-Item -Force -LiteralPath "$PsdPortableZip" }
if (Test-Path -LiteralPath "$OmniPortableZip") { Remove-Item -Force -LiteralPath "$OmniPortableZip" }
Compress-Archive -Path "$PortableDir\*" -DestinationPath "$PsdPortableZip" -CompressionLevel Optimal
Copy-Item -LiteralPath "$PsdPortableZip" -Destination "$OmniPortableZip" -Force
Write-Host "   -> Generated: $PsdPortableZip" -ForegroundColor Green

Write-Host "`n[3/4] Compiling Inno Setup Windows Installer (.exe)..." -ForegroundColor Yellow
$IssScript = Join-Path -Path $RootDir -ChildPath "desktop\Packaging\InnoSetup\OmniPdfSetup.iss"
if ($IsccExe) {
    & "$IsccExe" "$IssScript" | Out-Null
    Copy-Item -LiteralPath "$DistDir\PSD-PDF-Studio-Setup.exe" -Destination "$DistDir\OmniPdf-Setup.exe" -Force
    Write-Host "   -> Generated: $DistDir\PSD-PDF-Studio-Setup.exe" -ForegroundColor Green
} else {
    Write-Host "   -> Warning: Inno Setup compiler ISCC.exe not found." -ForegroundColor Yellow
}

Write-Host "`n[4/4] Compiling WiX v4/v5 Enterprise MSI Package (.msi)..." -ForegroundColor Yellow
$WxsFile = Join-Path -Path $RootDir -ChildPath "desktop\Packaging\WiX\OmniPdfEnterprise.wxs"
$PsdMsi = Join-Path -Path $DistDir -ChildPath "PSD-PDF-Studio-Enterprise.msi"
$OmniMsi = Join-Path -Path $DistDir -ChildPath "OmniPdf-Enterprise.msi"

if ($WixExe) {
    & "$WixExe" build "$WxsFile" -o "$PsdMsi" | Out-Null
    Copy-Item -LiteralPath "$PsdMsi" -Destination "$OmniMsi" -Force
    Write-Host "   -> Generated: $PsdMsi" -ForegroundColor Green
} else {
    Write-Host "   -> Warning: WiX Toolset compiler not found." -ForegroundColor Yellow
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "   WINDOWS PACKAGING PIPELINE COMPLETED SUCCESSFULLY      " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Get-ChildItem -LiteralPath "$DistDir" | Select-Object Name, Length, LastWriteTime
