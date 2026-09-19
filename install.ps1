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

$repoBase = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main"

Write-Host "  [*] Downloading application components..." -ForegroundColor Cyan

$files = @("WinTime.exe", "WinTime.ps1", "app.ico")
foreach ($f in $files) {
    $destName = Split-Path $f -Leaf
    Write-Host "      Downloading $destName ... " -NoNewline -ForegroundColor Gray
    try {
        Invoke-WebRequest -Uri "$repoBase/$f" -OutFile "$installDir\$destName" -UseBasicParsing -TimeoutSec 20
        Write-Host "OK" -ForegroundColor Green
    } catch {
        Write-Host "Skipped" -ForegroundColor DarkGray
    }
}

# Create Desktop Shortcut
$wsh = New-Object -ComObject WScript.Shell
$shortcutPath = "$env:USERPROFILE\Desktop\WinTime.lnk"
$shortcut = $wsh.CreateShortcut($shortcutPath)
if (Test-Path "$installDir\WinTime.exe") {
    $shortcut.TargetPath = "$installDir\WinTime.exe"
} else {
    $shortcut.TargetPath = "$installDir\WinTime.ps1"
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

if (Test-Path "$installDir\WinTime.exe") {
    Start-Process "$installDir\WinTime.exe"
} elseif (Test-Path "$installDir\WinTime.ps1") {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\WinTime.ps1`""
}
