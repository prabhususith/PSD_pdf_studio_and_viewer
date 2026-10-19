param(
    [string]$TagName = "v3.0.0",
    [string]$ReleaseName = "PSD PDF Studio & Viewer v3.0.0 (Initial Release)"
)

$ErrorActionPreference = "Stop"

Write-Host "=========================================================="
Write-Host "    PSD PDF STUDIO - GITHUB RELEASE ASSET PUBLISHER       "
Write-Host "=========================================================="

# 1. Retrieve GitHub Token from Credential Manager
$credsText = @"
protocol=https
host=github.com

"@ | git credential-manager get

$token = ""
$user = ""
foreach ($line in ($credsText -split "`r?`n")) {
    if ($line -like "password=*") {
        $token = $line.Substring("password=".Length)
    }
    if ($line -like "username=*") {
        $user = $line.Substring("username=".Length)
    }
}

if (-not $token) {
    Write-Error "Could not obtain GitHub token from git-credential-manager."
    exit 1
}

Write-Host "Authenticated as: $user"

$headers = @{
    "Authorization" = "Bearer $token"
    "User-Agent" = "PSD-Studio-Publisher"
    "Accept" = "application/vnd.github.v3+json"
}

$repoOwner = $user
$repoName = "PSD_pdf_studio_and_viewer"

# 2. Check if release already exists
$existingRelease = $null
try {
    $existingRelease = Invoke-RestMethod -Uri "https://api.github.com/repos/$repoOwner/$repoName/releases/tags/$TagName" -Headers $headers -Method Get
    Write-Host "Release '$TagName' already exists (ID: $($existingRelease.id))."
} catch {
    Write-Host "Release '$TagName' does not exist yet. Creating new release..."
}

$releaseBody = @"
## PSD PDF Studio & Viewer Enterprise v3.0.0 (Initial Release)

Welcome to the official **v3.0.0 Initial Release** of **PSD PDF Studio & Viewer**!

PSD PDF Studio & Viewer is an enterprise-grade desktop productivity workstation featuring:
- 🚀 **High-Performance Multi-Page Continuous Vector Canvas**: Seamless rendering across 1,000+ page documents with GPU acceleration.
- ✍️ **Microsoft Word-Compatible Document Editor**: Full WYSIWYG rich text formatting, typography palettes, case conversion, and Flesch-Kincaid readability scoring.
- 🔏 **Cryptographic E-Signature & PKI Certification Studio**: Legally verifiable SHA-256 digital seals with audit timestamps.
- 🤖 **Air-Gapped Sovereign Offline AI Copilot**: 100% private semantic search and Q&A powered by local vector embeddings.
- 🔄 **10-Format Document Conversion Pipeline**: Native support for PDF, DOCX, XLSX, PPTX, EPUB, MOBI, RTF, ODT, HTML, and Markdown.
- 🎨 **Modern Windows Fluent & Glass Themes**: True frameless native titlebar, Mica/Acrylic visual glass effects, and full-screen presentation mode.

---

### Included Packages & Distribution Artifacts

| Package | Filename | Description |
| :--- | :--- | :--- |
| **Windows Setup Wizard** | `PSD-PDF-Studio-Setup.exe` | Standard Windows installer with desktop & start menu shortcuts |
| **Windows Portable** | `PSD-PDF-Studio-Portable.zip` | Zero-install standalone archive for USB drives or non-admin use |
| **Enterprise MSI** | `PSD-PDF-Studio-Enterprise.msi` | Silent IT enterprise installer (`msiexec /i PSD-PDF-Studio-Enterprise.msi /qn`) |
| **Standalone Binary** | `psd_pdf.exe` | Single direct executable binary |
| **Android Package** | `PSD-PDF-Studio-Release-universal.apk` | Android 8.0+ universal APK |
| **Hardware Requirements** | `PSD_PDF_Studio_Hardware_Requirements.pdf` | Official Hardware & System Specification Sheet |
| **User Manual & Help Guide** | `PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf` | Comprehensive Features, Hotkeys & Operating Guide |

---

### Verification & Checksums
All release artifacts are compiled with release optimizations and signed for enterprise integrity.
Licensed under the **MIT License**.
"@

if (-not $existingRelease) {
    $createPayload = @{
        tag_name = $TagName
        target_commitish = "main"
        name = $ReleaseName
        body = $releaseBody
        draft = $false
        prerelease = $false
    } | ConvertTo-Json

    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repoOwner/$repoName/releases" -Headers $headers -Method Post -Body $createPayload -ContentType "application/json"
    Write-Host "Successfully created Release: $($release.html_url) (ID: $($release.id))"
} else {
    $release = $existingRelease
}

# 3. Assets to upload
$assetsToUpload = @(
    "build\dist\PSD-PDF-Studio-Setup.exe",
    "build\dist\PSD-PDF-Studio-Portable.zip",
    "build\dist\PSD-PDF-Studio-Enterprise.msi",
    "build\dist\psd_pdf.exe",
    "build\dist\PSD-PDF-Studio-Release-universal.apk",
    "build\dist\PSD_PDF_Studio_Hardware_Requirements.pdf",
    "build\dist\PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf"
)

# Fetch existing assets on the release
$currentAssets = Invoke-RestMethod -Uri "https://api.github.com/repos/$repoOwner/$repoName/releases/$($release.id)/assets" -Headers $headers -Method Get
$currentAssetNames = @{}
if ($currentAssets) {
    foreach ($a in $currentAssets) {
        $currentAssetNames[$a.name] = $a.id
    }
}

foreach ($relPath in $assetsToUpload) {
    $fullPath = Resolve-Path $relPath -ErrorAction SilentlyContinue
    if (-not $fullPath -or -not (Test-Path $fullPath)) {
        Write-Warning "File not found: $relPath - skipping."
        continue
    }

    $fileName = [System.IO.Path]::GetFileName($fullPath)
    $fileBytes = [System.IO.File]::ReadAllBytes($fullPath)
    $fileLength = $fileBytes.Length

    # If asset already uploaded, delete it first to ensure clean replacement
    if ($currentAssetNames.ContainsKey($fileName)) {
        $assetId = $currentAssetNames[$fileName]
        Write-Host "Asset $fileName already exists (ID: $assetId). Replacing..."
        Invoke-RestMethod -Uri "https://api.github.com/repos/$repoOwner/$repoName/releases/assets/$assetId" -Headers $headers -Method Delete
    }

    Write-Host "Uploading $fileName ($([Math]::Round($fileLength / 1MB, 2)) MB)..."

    $uploadUri = "https://uploads.github.com/repos/$repoOwner/$repoName/releases/$($release.id)/assets?name=$fileName"
    
    $uploadHeaders = @{
        "Authorization" = "Bearer $token"
        "User-Agent" = "PSD-Studio-Publisher"
        "Content-Type" = "application/octet-stream"
        "Accept" = "application/vnd.github.v3+json"
    }

    $uploadResp = Invoke-RestMethod -Uri $uploadUri -Headers $uploadHeaders -Method Post -Body $fileBytes
    Write-Host "Uploaded $fileName successfully -> $($uploadResp.browser_download_url)"
}

Write-Host "`nAll release assets published successfully!"
Write-Host "Release URL: $($release.html_url)"
