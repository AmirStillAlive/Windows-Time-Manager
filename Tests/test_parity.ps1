# WinTime CLI Parity Tests
# Validates behavioral parity between PowerShell CLI and C# Core
$ErrorActionPreference = 'Stop'

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "             WINTIME POWERSHELL PARITY TEST SUITE             " -ForegroundColor Yellow
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

$passed = 0
$failed = 0

function Invoke-ParityTest([string]$name, [scriptblock]$testBlock) {
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
    if ($f.Name -in @("ConvertTo-NtpTimestamp", "ConvertFrom-NtpTimestamp", "ConvertFrom-HttpDateHeader", "Get-HttpsTimeFromHost", "Test-PeerAddress")) {
        Invoke-Expression $f.Extent.Text
    }
}

# 1. Era 0 NTP Timestamp Test
Invoke-ParityTest "NTP Timestamp: Era 0 Round-trip (year 2026)" {
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
Invoke-ParityTest "NTP Timestamp: Era 1 Round-trip (year 2038 rollover)" {
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
Invoke-ParityTest "HTTP Date: RFC 1123 format parsing" {
    $rfc1123 = "Sun, 06 Nov 1994 08:49:37 GMT"
    $u = ConvertFrom-HttpDateHeader $rfc1123
    if ($null -eq $u) { throw "Failed to parse RFC 1123" }
    if ($u.Year -ne 1994 -or $u.Month -ne 11 -or $u.Day -ne 6 -or $u.Hour -ne 8 -or $u.Minute -ne 49 -or $u.Second -ne 37) {
        throw "Field mismatch: $u"
    }
}

# 4. HTTP Date Parsing: RFC 850
Invoke-ParityTest "HTTP Date: RFC 850 format parsing" {
    $rfc850 = "Sunday, 06-Nov-94 08:49:37 GMT"
    $u = ConvertFrom-HttpDateHeader $rfc850
    if ($null -eq $u) { throw "Failed to parse RFC 850" }
    if ($u.Year -ne 1994 -or $u.Month -ne 11 -or $u.Day -ne 6 -or $u.Hour -ne 8 -or $u.Minute -ne 49 -or $u.Second -ne 37) {
        throw "Field mismatch: $u"
    }
}

# 5. HTTP Date Parsing: ANSI C asctime()
Invoke-ParityTest "HTTP Date: ANSI C asctime format parsing" {
    $asctime = "Sun Nov  6 08:49:37 1994"
    $u = ConvertFrom-HttpDateHeader $asctime
    if ($null -eq $u) { throw "Failed to parse ANSI C asctime" }
    if ($u.Year -ne 1994 -or $u.Month -ne 11 -or $u.Day -ne 6 -or $u.Hour -ne 8 -or $u.Minute -ne 49 -or $u.Second -ne 37) {
        throw "Field mismatch: $u"
    }
}

# 6. HTTP Date Parsing: Negative / Garbage Input Handling
Invoke-ParityTest "HTTP Date: Garbage input returns `$null" {
    $garbage = "not a valid date string 12345"
    $u = ConvertFrom-HttpDateHeader $garbage
    if ($null -ne $u) { throw "Expected `$null for garbage input, got: $u" }

    $empty = ""
    $uEmpty = ConvertFrom-HttpDateHeader $empty
    if ($null -ne $uEmpty) { throw "Expected `$null for empty input, got: $uEmpty" }

    $nullInput = $null
    $uNull = ConvertFrom-HttpDateHeader $nullInput
    if ($null -ne $uNull) { throw "Expected `$null for null input, got: $uNull" }
}

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "PARITY RESULTS: $passed Passed, $failed Failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "==============================================================" -ForegroundColor Cyan

if ($failed -ne 0) { exit 1 }
