<#
.SYNOPSIS
    WinTime v0.1.3 - One-Liner Web Installer & Runner
.DESCRIPTION
    Run directly in PowerShell without manual downloading:
    irm https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/install.ps1 | iex
#>
$installerVersion = "0.1.3"

# ---- Administrator Elevation ----
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "  [*] Requesting Administrator privileges..." -ForegroundColor Yellow
    # Detect the URL this script was actually invoked from, defaulting to main branch
    $url = if ($MyInvocation.Line -match "https?://\S+") {
        $matches[0]
    } else {
        "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/install.ps1"
    }
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
if (-not (Test-Path -LiteralPath $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Enforce secure TLS protocols (TLS 1.2 and TLS 1.3 if supported)
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
if ([System.Enum]::IsDefined([System.Net.SecurityProtocolType], "Tls13")) {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls13
}

# Helper functions
function Test-UrlReachable([string]$testUrl) {
    try {
        $req = [System.Net.HttpWebRequest]::Create($testUrl)
        $req.Method = "HEAD"
        $req.Timeout = 5000
        $req.UserAgent = "WinTime-Installer"
        $resp = $req.GetResponse()
        $status = [int]$resp.StatusCode
        $resp.Close()
        return ($status -ge 200 -and $status -lt 400)
    } catch {
        return $false
    }
}

function Test-FileIntegrity {
    param(
        [string]$FilePath,
        [string]$FileName,
        [hashtable]$Checksums,
        [string]$SourceUrl
    )
    if (-not (Test-Path -LiteralPath $FilePath)) { return $false }
    if ($Checksums -and $Checksums.ContainsKey($FileName)) {
        $expected = $Checksums[$FileName].Trim().ToLowerInvariant()
        $actual = (Get-FileHash -LiteralPath $FilePath -Algorithm SHA256).Hash.Trim().ToLowerInvariant()
        if ($actual -ne $expected) {
            Write-Host "FAILED" -ForegroundColor Red
            Write-Host "  ================================================================" -ForegroundColor Red
            Write-Host "  [X] SECURITY ERROR: SHA-256 Checksum Mismatch for $FileName!" -ForegroundColor Red
            Write-Host "  ================================================================" -ForegroundColor Red
            Write-Host "      Source URL: $SourceUrl" -ForegroundColor Gray
            Write-Host "      Expected:   $expected" -ForegroundColor Yellow
            Write-Host "      Actual:     $actual" -ForegroundColor Red
            Write-Host "  [!] Maintainer hint: Check .gitattributes / line endings (LF vs CRLF) for this file." -ForegroundColor Yellow
            Write-Host "  ================================================================" -ForegroundColor Red
            Remove-Item -LiteralPath $FilePath -Force -ErrorAction SilentlyContinue
            return $false
        }
        Write-Host "OK (SHA-256 verified)" -ForegroundColor Green
        return $true
    } else {
        Write-Host "FAILED (Missing checksum in SHA256SUMS.txt)" -ForegroundColor Red
        Remove-Item -LiteralPath $FilePath -Force -ErrorAction SilentlyContinue
        return $false
    }
}

function Test-AuthenticodeSignatureStatus {
    param([string]$FilePath)
    if (-not (Test-Path -LiteralPath $FilePath)) { return }
    try {
        $sig = Get-AuthenticodeSignature -FilePath $FilePath -ErrorAction Stop
        if ($sig.Status -eq 'Valid') {
            Write-Host "  [+] Authenticode signature: Valid ($($sig.SignerCertificate.Subject))" -ForegroundColor Green
        } elseif ($sig.Status -eq 'NotSigned') {
            Write-Host "  [!] Warning: Binary is not code-signed. Integrity verified via SHA-256 only." -ForegroundColor Yellow
        } else {
            Write-Host "  [X] CRITICAL SECURITY ALERT: Invalid Authenticode signature (Status: $($sig.Status))!" -ForegroundColor Red
            Remove-Item -LiteralPath $FilePath -Force -ErrorAction SilentlyContinue
            throw "Authenticode signature validation failed for $FilePath (Status: $($sig.Status))"
        }
    } catch {
        if ($_.ToString() -match "Authenticode signature validation failed") { throw $_ }
        Write-Host "  [!] Warning: Could not check Authenticode signature: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# ---- Resolve Source Candidates ----
Write-Host "  [*] Resolving installation source..." -ForegroundColor Cyan

$candidateSources = @()

# 1. Check for release tag from GitHub Releases API
$releaseTag = $null
try {
    $apiResp = Invoke-RestMethod -Uri "https://api.github.com/repos/AmirStillAlive/Windows-Time-Manager/releases/latest" -Headers @{ "User-Agent" = "WinTime-Installer" } -TimeoutSec 5 -ErrorAction Stop
    if ($apiResp -and $apiResp.tag_name) {
        $releaseTag = $apiResp.tag_name
    }
} catch {
    Write-Host "      (GitHub Releases API unreachable or rate-limited; trying repository refs)" -ForegroundColor DarkGray
}

if ($releaseTag) {
    $candidateSources += @{
        Name = "GitHub Release Assets ($releaseTag)"
        Base = "https://github.com/AmirStillAlive/Windows-Time-Manager/releases/download/$releaseTag"
        Probe = "https://github.com/AmirStillAlive/Windows-Time-Manager/releases/download/$releaseTag/SHA256SUMS.txt"
    }
    $candidateSources += @{
        Name = "GitHub Raw Tag ($releaseTag)"
        Base = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/$releaseTag"
        Probe = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/$releaseTag/SHA256SUMS.txt"
    }
}

# 2. Add fallback release tag if API was unreachable
if (-not $releaseTag) {
    $candidateSources += @{
        Name = "GitHub Release Assets (v0.1.3)"
        Base = "https://github.com/AmirStillAlive/Windows-Time-Manager/releases/download/v0.1.3"
        Probe = "https://github.com/AmirStillAlive/Windows-Time-Manager/releases/download/v0.1.3/SHA256SUMS.txt"
    }
}

# 3. Add branch refs
$candidateSources += @{
    Name = "GitHub Raw main branch"
    Base = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main"
    Probe = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/SHA256SUMS.txt"
}
$candidateSources += @{
    Name = "GitHub Raw dev branch"
    Base = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/dev"
    Probe = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/dev/SHA256SUMS.txt"
}

# Attempt installation from candidates in sequence
$installedSuccessfully = $false
$exeDownloaded = $false

foreach ($src in $candidateSources) {
    Write-Host "  [*] Checking source: $($src.Name) ... " -NoNewline -ForegroundColor Gray
    if (-not (Test-UrlReachable $src.Probe)) {
        Write-Host "Unreachable" -ForegroundColor DarkGray
        continue
    }
    Write-Host "Reachable" -ForegroundColor Green

    $sourceBase = $src.Base
    Write-Host "  [*] Using Source Base: $sourceBase" -ForegroundColor Cyan

    # Step A: Download SHA256SUMS.txt from this source
    $sumsPath = "$installDir\SHA256SUMS.txt"
    $checksums = @{}
    try {
        Invoke-WebRequest -Uri "$sourceBase/SHA256SUMS.txt" -OutFile $sumsPath -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        if ((Test-Path -LiteralPath $sumsPath) -and (Get-Item -LiteralPath $sumsPath).Length -gt 0) {
            Get-Content -LiteralPath $sumsPath | ForEach-Object {
                if ($_ -match '^\s*([a-fA-F0-9]{64})\s+\*?(.+?)\s*$') {
                    $checksums[$matches[2].Trim()] = $matches[1].Trim()
                }
            }
        }
    } catch {
        Write-Host "  [!] Warning: Failed to retrieve SHA256SUMS.txt from $sourceBase" -ForegroundColor Yellow
    }

    if ($checksums.Count -eq 0) {
        Write-Host "  [X] Checksum file missing or empty from $sourceBase. Source rejected (fail-closed)." -ForegroundColor Red
        continue
    }

    # Step B: Download WinTime.exe (optional GUI binary)
    $exePath = "$installDir\WinTime.exe"
    Write-Host "      Downloading WinTime.exe ... " -NoNewline -ForegroundColor Gray
    $exeDownloaded = $false
    try {
        Invoke-WebRequest -Uri "$sourceBase/WinTime.exe" -OutFile $exePath -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
        if ((Test-Path -LiteralPath $exePath) -and (Get-Item -LiteralPath $exePath).Length -gt 10000) {
            $exeDownloaded = $true
            Write-Host "Downloaded ($((Get-Item -LiteralPath $exePath).Length) bytes), verifying ... " -NoNewline -ForegroundColor Gray
            if (-not (Test-FileIntegrity -FilePath $exePath -FileName "WinTime.exe" -Checksums $checksums -SourceUrl "$sourceBase/WinTime.exe")) {
                $exeDownloaded = $false
            }
            if ($exeDownloaded) {
                Unblock-File -LiteralPath $exePath -ErrorAction SilentlyContinue
                try {
                    Test-AuthenticodeSignatureStatus -FilePath $exePath
                } catch {
                    Write-Host "  [X] WinTime.exe rejected due to invalid signature." -ForegroundColor Red
                    $exeDownloaded = $false
                }
            }
        }
    } catch {
        Write-Host "Unavailable (Will use CLI engine)" -ForegroundColor DarkGray
        if (Test-Path -LiteralPath $exePath) { Remove-Item -LiteralPath $exePath -Force -ErrorAction SilentlyContinue }
    }

    # Step C: Download WinTime.ps1 (required CLI engine)
    $ps1Path = "$installDir\WinTime.ps1"
    Write-Host "      Downloading WinTime.ps1 ... " -NoNewline -ForegroundColor Gray
    $ps1Downloaded = $false
    try {
        Invoke-WebRequest -Uri "$sourceBase/WinTime.ps1" -OutFile $ps1Path -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
        if ((Test-Path -LiteralPath $ps1Path) -and (Get-Item -LiteralPath $ps1Path).Length -gt 1000) {
            $ps1Downloaded = $true
            if (-not (Test-FileIntegrity -FilePath $ps1Path -FileName "WinTime.ps1" -Checksums $checksums -SourceUrl "$sourceBase/WinTime.ps1")) {
                $ps1Downloaded = $false
            }
            if ($ps1Downloaded) {
                Unblock-File -LiteralPath $ps1Path -ErrorAction SilentlyContinue
            }
        }
    } catch {
        Write-Host "FAILED" -ForegroundColor Red
        Write-Host "  [!] Error downloading WinTime.ps1: $($_.Exception.Message)" -ForegroundColor Red
    }

    # Step D: Download WinTime.bat (required CLI launcher)
    $batPath = "$installDir\WinTime.bat"
    Write-Host "      Downloading WinTime.bat ... " -NoNewline -ForegroundColor Gray
    $batDownloaded = $false
    try {
        Invoke-WebRequest -Uri "$sourceBase/WinTime.bat" -OutFile $batPath -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
        if ((Test-Path -LiteralPath $batPath) -and (Get-Item -LiteralPath $batPath).Length -gt 50) {
            $batDownloaded = $true
            if (-not (Test-FileIntegrity -FilePath $batPath -FileName "WinTime.bat" -Checksums $checksums -SourceUrl "$sourceBase/WinTime.bat")) {
                $batDownloaded = $false
            }
            if ($batDownloaded) {
                Unblock-File -LiteralPath $batPath -ErrorAction SilentlyContinue
            }
        }
    } catch {
        Write-Host "Skipped" -ForegroundColor DarkGray
    }

    # Step E: Download app.ico (optional icon)
    $icoPath = "$installDir\app.ico"
    Write-Host "      Downloading app.ico ... " -NoNewline -ForegroundColor Gray
    try {
        Invoke-WebRequest -Uri "$sourceBase/app.ico" -OutFile $icoPath -UseBasicParsing -TimeoutSec 15 -ErrorAction SilentlyContinue
        if ((Test-Path -LiteralPath $icoPath) -and (Get-Item -LiteralPath $icoPath).Length -gt 100) {
            Write-Host "OK" -ForegroundColor Green
            Unblock-File -LiteralPath $icoPath -ErrorAction SilentlyContinue
        } else {
            Write-Host "Skipped" -ForegroundColor DarkGray
        }
    } catch {
        Write-Host "Skipped" -ForegroundColor DarkGray
    }

    # Check if this source successfully provided the runnable application
    if ($exeDownloaded -or ($ps1Downloaded -and $batDownloaded)) {
        $installedSuccessfully = $true
        break
    } else {
        Write-Host "  [!] Installation incomplete from $($src.Name). Falling back to next source..." -ForegroundColor Yellow
    }
}

if (-not $installedSuccessfully) {
    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor Red
    Write-Host "  [X] CRITICAL INSTALLATION FAILURE" -ForegroundColor Red
    Write-Host "  ================================================================" -ForegroundColor Red
    Write-Host "  Could not retrieve and verify application components from any source." -ForegroundColor Red
    Write-Host "  Please check your internet connection or download manually from:" -ForegroundColor Yellow
    Write-Host "  https://github.com/AmirStillAlive/Windows-Time-Manager" -ForegroundColor Cyan
    exit 1
}

# ---- Create Shortcuts & Uninstaller ----
$wsh = New-Object -ComObject WScript.Shell

# 1. Desktop Shortcut
$desktopShortcutPath = "$env:USERPROFILE\Desktop\WinTime.lnk"
$desktopShortcut = $wsh.CreateShortcut($desktopShortcutPath)
if ($exeDownloaded) {
    $desktopShortcut.TargetPath = "$installDir\WinTime.exe"
} elseif (Test-Path -LiteralPath "$installDir\WinTime.bat") {
    $desktopShortcut.TargetPath = "$installDir\WinTime.bat"
} else {
    $desktopShortcut.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    $desktopShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\WinTime.ps1`""
}
$desktopShortcut.WorkingDirectory = $installDir
if (Test-Path -LiteralPath "$installDir\app.ico") {
    $desktopShortcut.IconLocation = "$installDir\app.ico, 0"
}
$desktopShortcut.Description = "WinTime - Windows Time & NTP Manager"
$desktopShortcut.Save()

# 2. Start Menu Shortcuts
$startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\WinTime"
if (-not (Test-Path -LiteralPath $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
}

$startMenuShortcutPath = "$startMenuDir\WinTime.lnk"
$startMenuShortcut = $wsh.CreateShortcut($startMenuShortcutPath)
if ($exeDownloaded) {
    $startMenuShortcut.TargetPath = "$installDir\WinTime.exe"
} elseif (Test-Path -LiteralPath "$installDir\WinTime.bat") {
    $startMenuShortcut.TargetPath = "$installDir\WinTime.bat"
} else {
    $startMenuShortcut.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
    $startMenuShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\WinTime.ps1`""
}
$startMenuShortcut.WorkingDirectory = $installDir
if (Test-Path -LiteralPath "$installDir\app.ico") {
    $startMenuShortcut.IconLocation = "$installDir\app.ico, 0"
}
$startMenuShortcut.Description = "WinTime - Windows Time & NTP Manager"
$startMenuShortcut.Save()

# 3. Create uninstall.ps1 and Start Menu Uninstall Shortcut
$uninstallerPath = "$installDir\uninstall.ps1"
$uninstallerContent = @"
# WinTime Uninstaller
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "Uninstalling WinTime..." -ForegroundColor Yellow

`$installDir = "$env:LOCALAPPDATA\WinTime"
`$desktopLnk = "$env:USERPROFILE\Desktop\WinTime.lnk"
`$startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\WinTime"

if (Test-Path -LiteralPath `$desktopLnk) {
    Remove-Item -LiteralPath `$desktopLnk -Force -ErrorAction SilentlyContinue
    Write-Host "  [-] Removed Desktop shortcut." -ForegroundColor Gray
}

if (Test-Path -LiteralPath `$startMenuDir) {
    Remove-Item -LiteralPath `$startMenuDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "  [-] Removed Start Menu shortcuts." -ForegroundColor Gray
}

if (Test-Path -LiteralPath `$installDir) {
    Start-Process cmd.exe -ArgumentList "/c timeout /t 1 /nobreak >nul & rd /s /q `"`$installDir`"" -WindowStyle Hidden
    Write-Host "  [-] Removed WinTime directory." -ForegroundColor Gray
}

Write-Host ""
Write-Host "  [OK] WinTime has been successfully uninstalled." -ForegroundColor Green
Start-Sleep -Seconds 2
"@
[System.IO.File]::WriteAllText($uninstallerPath, $uninstallerContent, (New-Object System.Text.UTF8Encoding $false))
Unblock-File -LiteralPath $uninstallerPath -ErrorAction SilentlyContinue

$uninstallLnkPath = "$startMenuDir\Uninstall WinTime.lnk"
$uninstallLnk = $wsh.CreateShortcut($uninstallLnkPath)
$uninstallLnk.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$uninstallLnk.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""
$uninstallLnk.WorkingDirectory = $installDir
$uninstallLnk.Description = "Uninstall WinTime"
$uninstallLnk.Save()

Write-Host ""
Write-Host "  [OK] Successfully installed to: $installDir" -ForegroundColor Green
Write-Host "  [OK] Desktop shortcut created: $desktopShortcutPath" -ForegroundColor Green
Write-Host "  [OK] Start Menu shortcut created: $startMenuShortcutPath" -ForegroundColor Green
Write-Host ""
Write-Host "  [*] Launching WinTime..." -ForegroundColor Cyan

if ($exeDownloaded) {
    Start-Process "$installDir\WinTime.exe"
} elseif (Test-Path -LiteralPath "$installDir\WinTime.bat") {
    Start-Process "$installDir\WinTime.bat"
} else {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\WinTime.ps1`""
}
