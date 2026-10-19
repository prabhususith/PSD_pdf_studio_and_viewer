<#
.SYNOPSIS
    Repository Initialization Script for Omni PDF Desktop & Mobile
    Initializes git repository, sets up .gitignore, and verifies project prerequisites.
#>

param(
    [string]$RootDir = "P:\prabhu\projects\git\Omni PDF Desktop"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "    OMNI PDF STUDIO - REPOSITORY INITIALIZATION           " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Preserve whitespace integrity in directory navigation
Set-Location -LiteralPath "$RootDir"
Write-Host "[1/4] Checking Working Directory: $RootDir" -ForegroundColor Green

# 1. Initialize Git Repository if not present
if (-not (Test-Path -LiteralPath "$RootDir\.git")) {
    Write-Host "[2/4] Initializing Git Repository..." -ForegroundColor Yellow
    git init
    git branch -M main
} else {
    Write-Host "[2/4] Git repository already initialized." -ForegroundColor Green
}

# 2. Write standard comprehensive .gitignore
Write-Host "[3/4] Generating .gitignore manifest..." -ForegroundColor Yellow
$gitIgnoreContent = @"
# Miscellaneous
*.class
*.log
*.pyc
*.swp
.DS_Store
.atom/
.buildlog/
.history
.svn/
migrate_working_dir/

# Build artifacts
build/
build_temp/
dist/
bin/
obj/
*.apk
*.aab
*.msi
*.exe.intermediate
*.pdb

# Flutter / Dart
.dart_tool/
.flutter-plugins
.flutter-plugins-dependencies
.packages
.pub-cache/
.pub/
/ios/.generated/

# Android
android/local.properties
android/.gradle/
android/captures/
android/gradleBuild/
android/app/build/
android/app/intermediates/
android/app/generated/
**/GeneratedPluginRegistrant.java

# Visual Studio / C++
.vs/
*.vcxproj.user
*.VC.db
*.opendb
x64/
Debug/
Release/

# InnoSetup & WiX
*.cab
*.wixobj
*.wixpdb
"@

Set-Content -LiteralPath "$RootDir\.gitignore" -Value $gitIgnoreContent -Encoding UTF8

# 3. Ensure required directory blueprint exists
Write-Host "[4/4] Verifying Directory Scaffolding Blueprint..." -ForegroundColor Yellow
$requiredDirs = @(
    "core\include",
    "core\src",
    "desktop\Packaging\InnoSetup",
    "desktop\Packaging\WiX",
    "desktop\runner",
    "android\app\src\main\jniLibs\armeabi-v7a",
    "android\app\src\main\jniLibs\arm64-v8a",
    "android\app\src\main\java\com\omni\pdf\scanner",
    "android\app\src\main\java\com\omni\pdf\converters",
    "lib\core",
    "lib\state",
    "lib\ui\viewer",
    "lib\ui\tools",
    "lib\ui\editor",
    "lib\ui\themes",
    "lib\ui\scanner",
    "build\dist",
    "scripts"
)

foreach ($dir in $requiredDirs) {
    $fullPath = Join-Path -Path $RootDir -ChildPath $dir
    if (-not (Test-Path -LiteralPath "$fullPath")) {
        New-Item -ItemType Directory -Force -Path "$fullPath" | Out-Null
    }
}

Write-Host "Repository initialized successfully. All scaffolding verified." -ForegroundColor Green
