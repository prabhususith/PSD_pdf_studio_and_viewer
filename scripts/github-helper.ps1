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

Write-Host "Found User: $user"
$headers = @{
    "Authorization" = "Bearer $token"
    "User-Agent" = "PSD-App"
    "Accept" = "application/vnd.github.v3+json"
}

try {
    $resp = Invoke-WebRequest -Uri "https://api.github.com/user" -Headers $headers -Method Get
    $userInfo = $resp.Content | ConvertFrom-Json
    Write-Host "GitHub Auth Success! Logged in as: $($userInfo.login) ($($userInfo.name))"
    Write-Host "Scopes: $($resp.Headers['X-OAuth-Scopes'])"
    
    # Check if repo PSD_pdf_studio_and_viewer exists
    try {
        $repo = Invoke-RestMethod -Uri "https://api.github.com/repos/$($userInfo.login)/PSD_pdf_studio_and_viewer" -Headers $headers -Method Get
        Write-Host "Repo already exists: $($repo.html_url)"
    } catch {
        Write-Host "Repo PSD_pdf_studio_and_viewer does not exist yet. Creating repository..."
        $body = @{
            name = "PSD_pdf_studio_and_viewer"
            description = "PSD PDF Studio & Viewer - High-Performance Vector PDF Engine, Word Suite & Offline AI Copilot"
            private = $false
            has_issues = $true
            has_projects = $true
            has_wiki = $true
        } | ConvertTo-Json
        $newRepo = Invoke-RestMethod -Uri "https://api.github.com/user/repos" -Headers $headers -Method Post -Body $body -ContentType "application/json"
        Write-Host "Created repository: $($newRepo.html_url)"
    }
} catch {
    Write-Host "Error: $_"
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Host "Details: $($reader.ReadToEnd())"
    }
}
