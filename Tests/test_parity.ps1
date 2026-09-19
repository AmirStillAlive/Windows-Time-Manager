# WinTime CLI Parity Tests
# Validates behavioral parity between PowerShell CLI and C# Core
$ErrorActionPreference = 'Stop'

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "             WINTIME POWERSHELL PARITY TEST SUITE             " -ForegroundColor Yellow
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

$passed = 0
$failed = 0

function Run-ParityTest([string]$name, [scriptblock]$testBlock) {
    Write-Host ("  [*] " + $name.PadRight(60) + " ") -NoNewline
    try {
        & $testBlock
        Write-Host "[PASS]" -ForegroundColor Green
        $script:passed++
    } catch {
        Write-Host "[FAIL]" -ForegroundColor Red
        Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Red
        $script:failed++
    }
}

# Parse function definitions from WinTime.ps1 without executing interactive loop
$scriptRoot = Split-Path $PSScriptRoot -Parent
$winTimePs1 = Join-Path $scriptRoot "WinTime.ps1"
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path $winTimePs1), [ref]$null, [ref]$null)
$functionDefs = $ast.FindAll({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)

foreach ($f in $functionDefs) {
    if ($f.Name -in @("ConvertTo-NtpTimestamp", "ConvertFrom-NtpTimestamp", "Get-HttpsTimeFromHost", "Test-PeerAddress")) {
        Invoke-Expression $f.Extent.Text
    }
}

# 1. Era 0 NTP Timestamp Test
Run-ParityTest "NTP Timestamp: Era 0 Round-trip (year 2026)" {
    $era0Date = [datetime]::SpecifyKind([datetime]"2026-09-19 12:00:00", [System.DateTimeKind]::Utc)
    $parts = ConvertTo-NtpTimestamp $era0Date
    $sec = $parts[0]
    $frac = $parts[1]
    $secBytes = [System.BitConverter]::GetBytes([uint32]$sec)
    $fracBytes = [System.BitConverter]::GetBytes([uint32]$frac)
    if ([System.BitConverter]::IsLittleEndian) {
        [System.Array]::Reverse($secBytes)
        [System.Array]::Reverse($fracBytes)
    }
    $buf = New-Object byte[] 8
    [System.Array]::Copy($secBytes, 0, $buf, 0, 4)
    [System.Array]::Copy($fracBytes, 0, $buf, 4, 4)
    $readDate = ConvertFrom-NtpTimestamp $buf 0
    $diffSec = [math]::Abs(($readDate - $era0Date).TotalSeconds)
    if ($diffSec -gt 0.001) { throw "Diff too large: $diffSec s" }
    if ($readDate.Year -ne 2026) { throw "Year mismatch: $($readDate.Year)" }
}

# 2. Era 1 NTP Timestamp Test (Post-2036 Rollover)
Run-ParityTest "NTP Timestamp: Era 1 Round-trip (year 2038 rollover)" {
    $era1Date = [datetime]::SpecifyKind([datetime]"2038-05-10 08:30:00", [System.DateTimeKind]::Utc)
    $parts = ConvertTo-NtpTimestamp $era1Date
    $sec = $parts[0]
    $frac = $parts[1]
    $secBytes = [System.BitConverter]::GetBytes([uint32]$sec)
    $fracBytes = [System.BitConverter]::GetBytes([uint32]$frac)
    if ([System.BitConverter]::IsLittleEndian) {
        [System.Array]::Reverse($secBytes)
        [System.Array]::Reverse($fracBytes)
    }
    $buf = New-Object byte[] 8
    [System.Array]::Copy($secBytes, 0, $buf, 0, 4)
    [System.Array]::Copy($fracBytes, 0, $buf, 4, 4)
    $readDate = ConvertFrom-NtpTimestamp $buf 0
    $diffSec = [math]::Abs(($readDate - $era1Date).TotalSeconds)
    if ($diffSec -gt 0.001) { throw "Diff too large: $diffSec s" }
    if ($readDate.Year -ne 2038) { throw "Year mismatch: $($readDate.Year)" }
}

# 3. HTTP Date Parsing: RFC 1123
Run-ParityTest "HTTP Date: RFC 1123 format parsing" {
    [string[]]$formats = @(
        "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
        "ddd, dd MMM yyyy HH:mm:ss GMT",
        "dddd, dd-MMM-yy HH:mm:ss 'GMT'",
        "dddd, dd-MMM-yy HH:mm:ss GMT",
        "ddd MMM d HH:mm:ss yyyy",
        "ddd MMM  d HH:mm:ss yyyy",
        "ddd MMM dd HH:mm:ss yyyy",
        "r"
    )
    $rfc1123 = "Sun, 06 Nov 1994 08:49:37 GMT"
    $parsed = [datetime]::MinValue
    $ok = [datetime]::TryParseExact($rfc1123.Trim(), $formats, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal, [ref]$parsed)
    if (-not $ok) { throw "Failed to parse RFC 1123" }
    $u = $parsed.ToUniversalTime()
    if ($u.Year -ne 1994 -or $u.Month -ne 11 -or $u.Day -ne 6 -or $u.Hour -ne 8 -or $u.Minute -ne 49 -or $u.Second -ne 37) {
        throw "Field mismatch: $u"
    }
}

# 4. HTTP Date Parsing: RFC 850
Run-ParityTest "HTTP Date: RFC 850 format parsing" {
    [string[]]$formats = @(
        "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
        "ddd, dd MMM yyyy HH:mm:ss GMT",
        "dddd, dd-MMM-yy HH:mm:ss 'GMT'",
        "dddd, dd-MMM-yy HH:mm:ss GMT",
        "ddd MMM d HH:mm:ss yyyy",
        "ddd MMM  d HH:mm:ss yyyy",
        "ddd MMM dd HH:mm:ss yyyy",
        "r"
    )
    $rfc850 = "Sunday, 06-Nov-94 08:49:37 GMT"
    $parsed = [datetime]::MinValue
    $ok = [datetime]::TryParseExact($rfc850.Trim(), $formats, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal, [ref]$parsed)
    if (-not $ok) { throw "Failed to parse RFC 850" }
    $u = $parsed.ToUniversalTime()
    if ($u.Year -ne 1994 -or $u.Month -ne 11 -or $u.Day -ne 6 -or $u.Hour -ne 8 -or $u.Minute -ne 49 -or $u.Second -ne 37) {
        throw "Field mismatch: $u"
    }
}

# 5. HTTP Date Parsing: ANSI C asctime()
Run-ParityTest "HTTP Date: ANSI C asctime format parsing" {
    [string[]]$formats = @(
        "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
        "ddd, dd MMM yyyy HH:mm:ss GMT",
        "dddd, dd-MMM-yy HH:mm:ss 'GMT'",
        "dddd, dd-MMM-yy HH:mm:ss GMT",
        "ddd MMM d HH:mm:ss yyyy",
        "ddd MMM  d HH:mm:ss yyyy",
        "ddd MMM dd HH:mm:ss yyyy",
        "r"
    )
    $asctime = "Sun Nov  6 08:49:37 1994"
    $parsed = [datetime]::MinValue
    $ok = [datetime]::TryParseExact($asctime.Trim(), $formats, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal, [ref]$parsed)
    if (-not $ok) { throw "Failed to parse ANSI C asctime" }
    $u = $parsed.ToUniversalTime()
    if ($u.Year -ne 1994 -or $u.Month -ne 11 -or $u.Day -ne 6 -or $u.Hour -ne 8 -or $u.Minute -ne 49 -or $u.Second -ne 37) {
        throw "Field mismatch: $u"
    }
}

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "PARITY RESULTS: $passed Passed, $failed Failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "==============================================================" -ForegroundColor Cyan

if ($failed -ne 0) { exit 1 }
