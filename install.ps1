<#
.SYNOPSIS
    WinTime - One-Liner Web Installer & Runner
.DESCRIPTION
    Run directly in PowerShell without manual downloading:
    irm https://raw.githubusercontent.com/<username>/Windows-Time-Manager/main/install.ps1 | iex
#>

# ---- Administrator Elevation ----
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "  [*] Requesting Administrator privileges..." -ForegroundColor Yellow
    $url = if ($MyInvocation.Line -match "https?://\S+") { $matches[0] } else { "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/install.ps1" }
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"irm $url | iex`"" -Verb RunAs
    exit
}

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Clear-Host

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "                  WINTIME - ONE-CLICK INSTALLER                 " -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

$installDir = "$env:LOCALAPPDATA\WinTime"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

$version = "v0.1.0"
$rawBase = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/$version"
$releaseBase = "https://github.com/AmirStillAlive/Windows-Time-Manager/releases/download/$version"

Write-Host "  [*] Target Installation Directory: $installDir" -ForegroundColor Gray
Write-Host "  [*] Downloading application components ($version)..." -ForegroundColor Cyan

# Download WinTime.exe from GitHub Releases
$exePath = "$installDir\WinTime.exe"
Write-Host "      Downloading WinTime.exe ... " -NoNewline -ForegroundColor Gray
$exeDownloaded = $false
try {
    # Attempt download from GitHub Releases asset first
    Invoke-WebRequest -Uri "$releaseBase/WinTime.exe" -OutFile $exePath -UseBasicParsing -TimeoutSec 30
    if ((Test-Path $exePath) -and (Get-Item $exePath).Length -gt 10000) {
        $exeDownloaded = $true
        Write-Host "OK ($((Get-Item $exePath).Length) bytes)" -ForegroundColor Green
    }
} catch {}

if (-not $exeDownloaded) {
    try {
        # Fallback to raw repository branch/tag
        Invoke-WebRequest -Uri "$rawBase/WinTime.exe" -OutFile $exePath -UseBasicParsing -TimeoutSec 30
        if ((Test-Path $exePath) -and (Get-Item $exePath).Length -gt 10000) {
            $exeDownloaded = $true
            Write-Host "OK ($((Get-Item $exePath).Length) bytes)" -ForegroundColor Green
        }
    } catch {}
}

if (-not $exeDownloaded) {
    Write-Host "Unavailable (Will use PowerShell CLI engine)" -ForegroundColor Yellow
    if (Test-Path $exePath) { Remove-Item $exePath -Force -ErrorAction SilentlyContinue }
}

# Download WinTime.ps1 (CLI engine - required fallback)
$ps1Path = "$installDir\WinTime.ps1"
Write-Host "      Downloading WinTime.ps1 ... " -NoNewline -ForegroundColor Gray
$ps1Downloaded = $false
try {
    Invoke-WebRequest -Uri "$rawBase/WinTime.ps1" -OutFile $ps1Path -UseBasicParsing -TimeoutSec 20
    if ((Test-Path $ps1Path) -and (Get-Item $ps1Path).Length -gt 1000) {
        $ps1Downloaded = $true
        Write-Host "OK ($((Get-Item $ps1Path).Length) bytes)" -ForegroundColor Green
    } else {
        throw "Downloaded file is empty or incomplete."
    }
} catch {
    Write-Host "FAILED" -ForegroundColor Red
    Write-Host "  [X] CRITICAL ERROR: Failed to download WinTime.ps1: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $exeDownloaded) {
        Write-Host "  [X] Installation aborted: Neither native binary nor CLI script could be retrieved." -ForegroundColor Red
        exit 1
    }
}

# Download app.ico (optional icon)
$icoPath = "$installDir\app.ico"
Write-Host "      Downloading app.ico ... " -NoNewline -ForegroundColor Gray
try {
    Invoke-WebRequest -Uri "$rawBase/app.ico" -OutFile $icoPath -UseBasicParsing -TimeoutSec 15
    if ((Test-Path $icoPath) -and (Get-Item $icoPath).Length -gt 100) {
        Write-Host "OK" -ForegroundColor Green
    } else {
        Write-Host "Skipped" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "Skipped" -ForegroundColor DarkGray
}

# Create Desktop Shortcut
$wsh = New-Object -ComObject WScript.Shell
$shortcutPath = "$env:USERPROFILE\Desktop\WinTime.lnk"
$shortcut = $wsh.CreateShortcut($shortcutPath)
if ($exeDownloaded) {
    $shortcut.TargetPath = "$installDir\WinTime.exe"
} else {
    $shortcut.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    $shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\WinTime.ps1`""
}
$shortcut.WorkingDirectory = $installDir
if (Test-Path "$installDir\app.ico") {
    $shortcut.IconLocation = "$installDir\app.ico, 0"
}
$shortcut.Description = "WinTime - Windows Time & NTP Manager"
$shortcut.Save()

Write-Host ""
Write-Host "  [OK] Successfully installed to: $installDir" -ForegroundColor Green
Write-Host "  [✓] Desktop shortcut created: $shortcutPath" -ForegroundColor Green
Write-Host ""
Write-Host "  [*] Launching WinTime..." -ForegroundColor Cyan

if ($exeDownloaded) {
    Start-Process "$installDir\WinTime.exe"
} else {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\WinTime.ps1`""
}
