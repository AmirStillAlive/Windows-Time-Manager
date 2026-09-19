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

# Enforce secure TLS protocols (TLS 1.2 and TLS 1.3 if supported)
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
if ([System.Enum]::IsDefined([System.Net.SecurityProtocolType], "Tls13")) {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls13
}

$defaultVersion = "v0.1.3"
$version = $defaultVersion
try {
    $releaseJson = Invoke-RestMethod -Uri "https://api.github.com/repos/AmirStillAlive/Windows-Time-Manager/releases/latest" -Headers @{ "User-Agent" = "WinTime-Installer" } -TimeoutSec 5 -ErrorAction Stop
    if ($releaseJson.tag_name) {
        $version = $releaseJson.tag_name
    }
} catch {
    # Fallback to hardcoded default version if offline, rate-limited, or tag unavailable
    $version = $defaultVersion
}

$rawBase = "https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/$version"
$releaseBase = "https://github.com/AmirStillAlive/Windows-Time-Manager/releases/download/$version"

Write-Host "  [*] Target Installation Directory: $installDir" -ForegroundColor Gray
Write-Host "  [*] Downloading application components ($version)..." -ForegroundColor Cyan

# Helper functions for integrity verification
function Verify-FileIntegrity {
    param(
        [string]$FilePath,
        [string]$FileName,
        [hashtable]$Checksums
    )
    if (-not (Test-Path $FilePath)) { return $false }
    if ($Checksums.ContainsKey($FileName)) {
        $expected = $Checksums[$FileName]
        $actual = (Get-FileHash -Path $FilePath -Algorithm SHA256).Hash
        if ($actual.ToLowerInvariant() -ne $expected.ToLowerInvariant()) {
            Write-Host "FAILED (SHA-256 mismatch!)" -ForegroundColor Red
            Write-Host "  [X] SECURITY ALERT: Hash mismatch for $FileName!" -ForegroundColor Red
            Write-Host "      Expected: $expected" -ForegroundColor Red
            Write-Host "      Actual:   $actual" -ForegroundColor Red
            Remove-Item $FilePath -Force -ErrorAction SilentlyContinue
            return $false
        }
        Write-Host "OK (SHA-256 verified)" -ForegroundColor Green
        return $true
    } else {
        Write-Host "OK (No hash in SHA256SUMS.txt)" -ForegroundColor Yellow
        return $true
    }
}

function Check-AuthenticodeSignature {
    param([string]$FilePath)
    if (-not (Test-Path $FilePath)) { return }
    try {
        $sig = Get-AuthenticodeSignature -FilePath $FilePath -ErrorAction Stop
        if ($sig.Status -eq 'Valid') {
            Write-Host "  [+] Authenticode signature: Valid ($($sig.SignerCertificate.Subject))" -ForegroundColor Green
        } elseif ($sig.Status -eq 'NotSigned') {
            Write-Host "  [!] Warning: Binary is not code-signed. Integrity verified via SHA-256 only." -ForegroundColor Yellow
        } else {
            Write-Host "  [X] CRITICAL SECURITY ALERT: Invalid Authenticode signature (Status: $($sig.Status))!" -ForegroundColor Red
            Remove-Item $FilePath -Force -ErrorAction SilentlyContinue
            throw "Authenticode signature validation failed for $FilePath (Status: $($sig.Status))"
        }
    } catch {
        if ($_ -match "Authenticode signature validation failed") { throw $_ }
        Write-Host "  [!] Warning: Could not check Authenticode signature: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Download SHA256SUMS.txt
$sumsPath = "$installDir\SHA256SUMS.txt"
$checksums = @{}
$sumsDownloaded = $false

try {
    Invoke-WebRequest -Uri "$releaseBase/SHA256SUMS.txt" -OutFile $sumsPath -UseBasicParsing -TimeoutSec 15
    if ((Test-Path $sumsPath) -and (Get-Item $sumsPath).Length -gt 0) { $sumsDownloaded = $true }
} catch {}

if (-not $sumsDownloaded) {
    try {
        Invoke-WebRequest -Uri "$rawBase/SHA256SUMS.txt" -OutFile $sumsPath -UseBasicParsing -TimeoutSec 15
        if ((Test-Path $sumsPath) -and (Get-Item $sumsPath).Length -gt 0) { $sumsDownloaded = $true }
    } catch {}
}

if ($sumsDownloaded) {
    Get-Content $sumsPath | ForEach-Object {
        if ($_ -match '^\s*([a-fA-F0-9]{64})\s+\*?(.+?)\s*$') {
            $checksums[$matches[2].Trim()] = $matches[1].Trim()
        }
    }
} else {
    Write-Host "  [!] Warning: Could not retrieve SHA256SUMS.txt. Integrity verification will be degraded." -ForegroundColor Yellow
}

# Download WinTime.exe from GitHub Releases
$exePath = "$installDir\WinTime.exe"
Write-Host "      Downloading WinTime.exe ... " -NoNewline -ForegroundColor Gray
$exeDownloaded = $false
try {
    # Attempt download from GitHub Releases asset first
    Invoke-WebRequest -Uri "$releaseBase/WinTime.exe" -OutFile $exePath -UseBasicParsing -TimeoutSec 30
    if ((Test-Path $exePath) -and (Get-Item $exePath).Length -gt 10000) {
        $exeDownloaded = $true
        Write-Host "Downloaded ($((Get-Item $exePath).Length) bytes), verifying ... " -NoNewline -ForegroundColor Gray
    }
} catch {}

if (-not $exeDownloaded) {
    try {
        # Fallback to raw repository branch/tag
        Invoke-WebRequest -Uri "$rawBase/WinTime.exe" -OutFile $exePath -UseBasicParsing -TimeoutSec 30
        if ((Test-Path $exePath) -and (Get-Item $exePath).Length -gt 10000) {
            $exeDownloaded = $true
            Write-Host "Downloaded ($((Get-Item $exePath).Length) bytes), verifying ... " -NoNewline -ForegroundColor Gray
        }
    } catch {}
}

if ($exeDownloaded) {
    if ($checksums.Count -gt 0) {
        $verified = Verify-FileIntegrity -FilePath $exePath -FileName "WinTime.exe" -Checksums $checksums
        if (-not $verified) {
            $exeDownloaded = $false
        }
    } else {
        Write-Host "OK (Unverified)" -ForegroundColor Yellow
    }

    if ($exeDownloaded) {
        try {
            Check-AuthenticodeSignature -FilePath $exePath
        } catch {
            Write-Host "  [X] Installation of WinTime.exe aborted due to signature failure." -ForegroundColor Red
            $exeDownloaded = $false
        }
    }
} else {
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
        if ($checksums.Count -gt 0) {
            $verified = Verify-FileIntegrity -FilePath $ps1Path -FileName "WinTime.ps1" -Checksums $checksums
            if (-not $verified) {
                $ps1Downloaded = $false
                throw "Integrity verification failed for WinTime.ps1"
            }
        } else {
            Write-Host "OK ($((Get-Item $ps1Path).Length) bytes)" -ForegroundColor Green
        }
    } else {
        throw "Downloaded file is empty or incomplete."
    }
} catch {
    Write-Host "FAILED" -ForegroundColor Red
    Write-Host "  [X] CRITICAL ERROR: Failed to download or verify WinTime.ps1: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $exeDownloaded) {
        Write-Host "  [X] Installation aborted: Neither native binary nor CLI script could be retrieved and verified." -ForegroundColor Red
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
Write-Host "  [OK] Desktop shortcut created: $shortcutPath" -ForegroundColor Green
Write-Host ""
Write-Host "  [*] Launching WinTime..." -ForegroundColor Cyan

if ($exeDownloaded) {
    Start-Process "$installDir\WinTime.exe"
} else {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$installDir\WinTime.ps1`""
}
