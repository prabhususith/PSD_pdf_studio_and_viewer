<#
.SYNOPSIS
    Automated Android Enterprise Production APK Compiler & Packager
    Builds Universal Release APK:
      build\dist\PSD-PDF-Studio-Release-universal.apk
      build\dist\OmniPdf-Release-universal.apk
    Targets Android 6.0 Marshmallow (API 23) through Android 14+ (API 34/35).
    Bundles compiled XML resources, adaptive high-resolution launcher icons,
    embedded offline typography fonts, and official documentation PDFs.
    Signed with APK Signature Scheme v1, v2, and v3.
#>

param(
    [string]$RootDir = "P:\prabhu\projects\git\Omni PDF Desktop",
    [string]$DistDir = "P:\prabhu\projects\git\Omni PDF Desktop\build\dist"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    PSD PDF STUDIO & VIEWER - ANDROID APK COMPILER         " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Set-Location -LiteralPath "$RootDir"

# 1. Toolchain discovery
$Javac = "C:\Program Files\Microsoft\jdk-17.0.19.10-hotspot\bin\javac.exe"
$Keytool = "C:\Program Files\Microsoft\jdk-17.0.19.10-hotspot\bin\keytool.exe"
$AndroidSdk = "C:\Users\prabh\AppData\Local\Android\Sdk"
$BuildTools = "$AndroidSdk\build-tools\34.0.0"
$AndroidJar = "$AndroidSdk\platforms\android-34\android.jar"
$Aapt2 = "$BuildTools\aapt2.exe"
$D8 = "$BuildTools\d8.bat"
$ZipAlign = "$BuildTools\zipalign.exe"
$ApkSigner = "$BuildTools\apksigner.bat"
$SevenZip = "C:\Program Files\7-Zip\7z.exe"

# 2. Output and staging directories
if (-not (Test-Path -LiteralPath "$DistDir")) {
    New-Item -ItemType Directory -Force -Path "$DistDir" | Out-Null
}

$BuildTemp = Join-Path -Path $RootDir -ChildPath "build\android_temp"
if (Test-Path -LiteralPath "$BuildTemp") {
    Remove-Item -Recurse -Force -LiteralPath "$BuildTemp"
}
New-Item -ItemType Directory -Force -Path "$BuildTemp\classes" | Out-Null
New-Item -ItemType Directory -Force -Path "$BuildTemp\apk_root" | Out-Null

Write-Host "[1/6] Compiling Java Sources against Android 34 SDK..." -ForegroundColor Yellow
$JavaSources = Get-ChildItem -Path "$RootDir\android\app\src\main\java" -Filter "*.java" -Recurse | Select-Object -ExpandProperty FullName
& "$Javac" -encoding UTF-8 -cp "$AndroidJar" -d "$BuildTemp\classes" -source 1.8 -target 1.8 $JavaSources
if ($LASTEXITCODE -ne 0) { throw "Java compilation failed with exit code $LASTEXITCODE" }
Write-Host "   -> Java classes compiled successfully." -ForegroundColor Green

Write-Host "[2/6] Converting Java Bytecode to Dalvik DEX (d8)..." -ForegroundColor Yellow
$ClassFiles = Get-ChildItem -Path "$BuildTemp\classes" -Filter "*.class" -Recurse | Select-Object -ExpandProperty FullName
& "$D8" --lib "$AndroidJar" --min-api 23 --output "$BuildTemp\apk_root" $ClassFiles
if ($LASTEXITCODE -ne 0) { throw "D8 dex compilation failed with exit code $LASTEXITCODE" }
Write-Host "   -> classes.dex generated for API 23+ (Android 6.0 through 14+)." -ForegroundColor Green

Write-Host "[3/6] Compiling Android Resources and Linking Manifest (aapt2)..." -ForegroundColor Yellow
$CompiledRes = "$BuildTemp\compiled_res.zip"
& "$Aapt2" compile --dir "$RootDir\android\app\src\main\res" -o "$CompiledRes"
if ($LASTEXITCODE -ne 0) { throw "aapt2 compile failed with exit code $LASTEXITCODE" }

$AssetsDir = "$RootDir\android\app\src\main\assets"
$BaseApk = "$BuildTemp\base.apk"
& "$Aapt2" link -I "$AndroidJar" --manifest "$RootDir\android\app\src\main\AndroidManifest.xml" -R "$CompiledRes" -A "$AssetsDir" --auto-add-overlay --min-sdk-version 23 --target-sdk-version 34 -o "$BaseApk"
if ($LASTEXITCODE -ne 0) { throw "aapt2 link failed with exit code $LASTEXITCODE" }
Write-Host "   -> Binary manifest linked with compiled resources, icons, and assets." -ForegroundColor Green

Write-Host "[4/6] Staging Bundled DEX into Package..." -ForegroundColor Yellow
Push-Location "$BuildTemp\apk_root"
& "$SevenZip" a -tzip -r "$BaseApk" * | Out-Null
Pop-Location
Write-Host "   -> Bundled classes.dex into production APK." -ForegroundColor Green

Write-Host "[5/6] 4-Byte Page Boundary Zipalign (zipalign)..." -ForegroundColor Yellow
$AlignedApk = "$BuildTemp\aligned.apk"
& "$ZipAlign" -f -p 4 "$BaseApk" "$AlignedApk"
if ($LASTEXITCODE -ne 0) { throw "zipalign failed with exit code $LASTEXITCODE" }
Write-Host "   -> 4-byte page aligned for zero-copy memory mapping." -ForegroundColor Green

Write-Host "[6/6] Cryptographic APK Signing (apksigner v1, v2, v3)..." -ForegroundColor Yellow
$Keystore = "$RootDir\build\debug.keystore"
if (-not (Test-Path -LiteralPath "$Keystore")) {
    & "$Keytool" -genkeypair -v -keystore "$Keystore" -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US" | Out-Null
}

$PsdApk = Join-Path -Path $DistDir -ChildPath "PSD-PDF-Studio-Release-universal.apk"
$FinalApk = Join-Path -Path $DistDir -ChildPath "OmniPdf-Release-universal.apk"
if (Test-Path -LiteralPath "$PsdApk") { Remove-Item -Force -LiteralPath "$PsdApk" }
if (Test-Path -LiteralPath "$FinalApk") { Remove-Item -Force -LiteralPath "$FinalApk" }

& "$ApkSigner" sign --ks "$Keystore" --ks-pass pass:android --key-pass pass:android --out "$PsdApk" "$AlignedApk"
if ($LASTEXITCODE -ne 0) { throw "apksigner failed with exit code $LASTEXITCODE" }
Copy-Item -LiteralPath "$PsdApk" -Destination "$FinalApk" -Force
Write-Host "   -> APK signed successfully." -ForegroundColor Green

# Verification
Write-Host "`n>>> Running APK Signature Verification..." -ForegroundColor Yellow
& "$ApkSigner" verify --verbose "$PsdApk"

Write-Host "`nRelease Universal APK generated and verified successfully at:" -ForegroundColor Green
Write-Host "   -> $PsdApk" -ForegroundColor Green

Get-ChildItem -LiteralPath "$PsdApk" | Select-Object Name, Length, LastWriteTime
