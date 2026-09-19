# WinTime CLI Engine (PowerShell)
# Lightweight Windows Time & NTP Management CLI

# ---- Self-Elevation (Administrator Check) ----
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "  [*] Requesting Administrator privileges..." -ForegroundColor Yellow
    $scriptPath = if ($PSCommandPath) { $PSCommandPath } else { $MyInvocation.MyCommand.Definition }
    if ($scriptPath -and (Test-Path $scriptPath)) {
        Start-Process powershell.exe -ArgumentList "-NoProfile -File `"$scriptPath`"" -Verb RunAs
    }
    exit
}

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Get-ConfiguredPeers {
    $list = @()
    try {
        $regVal = (Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\W32Time\Parameters' -ErrorAction SilentlyContinue).NtpServer
        if ($regVal) {
            foreach ($item in ($regVal -split '\s+' | Where-Object { $_ -match '\S' })) {
                $cleanHost = ($item -split ',')[0].Trim()
                $flag = ($item -split ',')[1]
                if ([string]::IsNullOrEmpty($flag)) { $flag = "0x8" }
                if ($cleanHost -and -not $cleanHost.Contains("?")) {
                    $list += [PSCustomObject]@{
                        Host = $cleanHost
                        Flag = $flag
                        Raw  = "$cleanHost,$flag"
                    }
                }
            }
        }
    } catch {}
    return $list
}

function Set-ConfiguredPeers($peerObjects) {
    if (-not $peerObjects -or $peerObjects.Count -eq 0) {
        Write-Host "  [X] Error: Peer list cannot be empty. Windows Time service requires at least one peer." -ForegroundColor Red
        return $false
    }

    # Domain join check
    try {
        $comp = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
        if ($comp -and $comp.PartOfDomain) {
            Write-Host "  [!] Notice: Machine is joined to domain '$($comp.Domain)'. Active Directory Domain Controllers" -ForegroundColor Yellow
            Write-Host "      may override manually configured NTP peers via Group Policy." -ForegroundColor Yellow
        }
    } catch {}

    $valStr = ($peerObjects | ForEach-Object { "$($_.Host),$($_.Flag)" }) -join " "
    try {
        # Never specify /reliable:yes on client workstations (only for authoritative DCs)
        $w32Out = w32tm /config /manualpeerlist:"$valStr" /syncfromflags:manual /update 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0 -and -not ($w32Out -match "command completed successfully")) {
            Write-Host "  [X] Error configuring peers via w32tm (Exit code $LASTEXITCODE): $($w32Out.Trim())" -ForegroundColor Red
            return $false
        }
        $scOut = sc.exe config w32time start= auto 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  [!] Warning configuring service start: $($scOut.Trim())" -ForegroundColor DarkGray
        }
        Restart-Service w32time -Force -ErrorAction SilentlyContinue
        return $true
    } catch {
        Write-Host "  [X] Error updating Windows Time Service: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Test-PeerAddress($raw) {
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @{ IsValid = $false; Error = "Server address cannot be empty." }
    }
    $str = $raw.Trim()

    # Reject non-ASCII characters
    foreach ($ch in $str.ToCharArray()) {
        if ([int]$ch -gt 127) {
            return @{ IsValid = $false; Error = "Non-ASCII characters (Persian/Arabic) are not allowed." }
        }
    }

    # IPv4 Check
    $ip = $null
    if ([System.Net.IPAddress]::TryParse($str, [ref]$ip)) {
        $ipStr = $ip.ToString()
        if ($ip.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) {
            $bytes = $ip.GetAddressBytes()
            if ($bytes[0] -eq 127) { return @{ IsValid = $false; Error = "Loopback address ($ipStr) cannot be used as NTP peer." } }
            if ($bytes[0] -eq 0) { return @{ IsValid = $false; Error = "Unspecified address ($ipStr) cannot be used as NTP peer." } }
            if ($bytes[0] -ge 224 -and $bytes[0] -le 239) { return @{ IsValid = $false; Error = "Multicast address ($ipStr) cannot be used as NTP peer." } }
            if ($ipStr -eq "255.255.255.255") { return @{ IsValid = $false; Error = "Broadcast address cannot be used as NTP peer." } }
            return @{ IsValid = $true; CleanHost = $ipStr }
        }
        if ($ip.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) {
            if ($ip.IsIPv6Multicast) { return @{ IsValid = $false; Error = "IPv6 multicast address ($ipStr) cannot be used as NTP peer." } }
            if ($ipStr -eq "::1" -or $ipStr -eq "0:0:0:0:0:0:0:1") { return @{ IsValid = $false; Error = "IPv6 loopback address ($ipStr) cannot be used as NTP peer." } }
            if ($ipStr -eq "::") { return @{ IsValid = $false; Error = "IPv6 unspecified address cannot be used as NTP peer." } }
            return @{ IsValid = $true; CleanHost = $ipStr }
        }
    }

    # Hostname validation
    if ($str -match "^localhost$" -or $str -match "\.localhost$") {
        return @{ IsValid = $false; Error = "Localhost domain cannot be used as NTP peer." }
    }

    if ($str.Length -gt 253) {
        return @{ IsValid = $false; Error = "Hostname exceeds maximum length of 253 characters." }
    }

    $labels = $str.Split('.')
    if ($labels.Length -lt 2) {
        return @{ IsValid = $false; Error = "Hostname must be a fully qualified domain name with at least one dot (e.g. pool.ntp.org)." }
    }

    foreach ($lbl in $labels) {
        if ($lbl.Length -lt 1 -or $lbl.Length -gt 63) {
            return @{ IsValid = $false; Error = "Domain label length must be between 1 and 63 characters." }
        }
        if ($lbl.StartsWith("-") -or $lbl.EndsWith("-")) {
            return @{ IsValid = $false; Error = "Domain label cannot start or end with a hyphen ('$lbl')." }
        }
        if (-not ($lbl -match "^[a-zA-Z0-9\-]+$")) {
            return @{ IsValid = $false; Error = "Domain label contains invalid characters ('$lbl')." }
        }
    }

    # TLD cannot be purely numeric
    $tld = $labels[$labels.Length - 1]
    if ($tld -match "^\d+$") {
        return @{ IsValid = $false; Error = "Top-level domain cannot be purely numeric ('$tld')." }
    }

    return @{ IsValid = $true; CleanHost = $str.ToLowerInvariant() }
}

function Show-Header {
    Clear-Host
    $now = Get-Date -Format "yyyy-MM-dd  HH:mm:ss"
    $tz = [System.TimeZoneInfo]::Local.DisplayName
    
    $peers = Get-ConfiguredPeers
    $peerCount = $peers.Count

    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "         WINTIME v0.1.2 (ALPHA) - WINDOWS TIME & NTP MANAGER" -ForegroundColor Yellow
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "   Current Time: " -ForegroundColor Gray -NoNewline
    Write-Host "$now" -ForegroundColor Yellow
    Write-Host "   Time Zone:    " -ForegroundColor Gray -NoNewline
    Write-Host "$tz" -ForegroundColor Gray
    Write-Host "   System Peers: " -ForegroundColor Gray -NoNewline
    Write-Host "$peerCount peer(s) configured in Windows" -ForegroundColor Green
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function ConvertTo-NtpTimestamp([datetime]$utcDate) {
    $epoch = [datetime]::SpecifyKind([datetime]"1900-01-01 00:00:00", [System.DateTimeKind]::Utc)
    $span = $utcDate - $epoch
    $sec = [uint32]$span.TotalSeconds
    $frac = [uint32](($span.TotalSeconds - [math]::Floor($span.TotalSeconds)) * 4294967296.0)
    return @($sec, $frac)
}

function ConvertFrom-NtpTimestamp([byte[]]$bytes, [int]$offset) {
    $sec = [uint32](([uint32]$bytes[$offset] -shl 24) -bor ([uint32]$bytes[$offset + 1] -shl 16) -bor ([uint32]$bytes[$offset + 2] -shl 8) -bor [uint32]$bytes[$offset + 3])
    $frac = [uint32](([uint32]$bytes[$offset + 4] -shl 24) -bor ([uint32]$bytes[$offset + 5] -shl 16) -bor ([uint32]$bytes[$offset + 6] -shl 8) -bor [uint32]$bytes[$offset + 7])
    if ($sec -eq 0 -and $frac -eq 0) { return $null }
    $epoch = [datetime]::SpecifyKind([datetime]"1900-01-01 00:00:00", [System.DateTimeKind]::Utc)
    $ms = ($frac * 1000.0) / 4294967296.0
    return $epoch.AddSeconds($sec).AddMilliseconds($ms)
}

function Get-NtpTimeFromHost($hostName, $timeoutMs = 2500) {
    $client = $null
    try {
        $client = New-Object System.Net.Sockets.UdpClient
        $client.Client.ReceiveTimeout = $timeoutMs
        $client.Client.SendTimeout = $timeoutMs
        $client.Connect($hostName, 123)

        $packet = New-Object byte[] 48
        # LI = 0, VN = 4, Mode = 3 (Client) -> 0x23
        $packet[0] = 0x23

        # Transmit timestamp (t1)
        $t1 = [datetime]::UtcNow
        $t1Parts = ConvertTo-NtpTimestamp $t1
        $secBytes = [System.BitConverter]::GetBytes([uint32]$t1Parts[0])
        $fracBytes = [System.BitConverter]::GetBytes([uint32]$t1Parts[1])
        if ([System.BitConverter]::IsLittleEndian) {
            [System.Array]::Reverse($secBytes)
            [System.Array]::Reverse($fracBytes)
        }
        [System.Array]::Copy($secBytes, 0, $packet, 40, 4)
        [System.Array]::Copy($fracBytes, 0, $packet, 44, 4)

        [void]$client.Send($packet, $packet.Length)
        $endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        $resp = $client.Receive([ref]$endpoint)
        $t4 = [datetime]::UtcNow

        if ($resp.Length -lt 48) {
            return @{ Success = $false; Host = $hostName; Error = "Malformed packet (length < 48 bytes)" }
        }

        # Header fields validation
        $li = ($resp[0] -band 0xC0) -shr 6
        $vn = ($resp[0] -band 0x38) -shr 3
        $mode = $resp[0] -band 0x07
        $stratum = [int]$resp[1]

        if ($li -eq 3) {
            return @{ Success = $false; Host = $hostName; Error = "Server unsynchronized (Alarm condition LI=3)" }
        }
        if ($vn -lt 3 -or $vn -gt 4) {
            return @{ Success = $false; Host = $hostName; Error = "Invalid NTP version ($vn)" }
        }
        if ($mode -ne 4 -and $mode -ne 5) {
            return @{ Success = $false; Host = $hostName; Error = "Invalid response mode ($mode)" }
        }
        if ($stratum -eq 0) {
            $kod = [System.Text.Encoding]::ASCII.GetString($resp, 12, 4)
            return @{ Success = $false; Host = $hostName; Error = "Kiss-o'-Death received ($kod)" }
        }
        if ($stratum -gt 15) {
            return @{ Success = $false; Host = $hostName; Error = "Server unsynchronized (Stratum $stratum)" }
        }

        # Nonce / Origin timestamp verification (bytes 24..31 must match packet bytes 40..47)
        for ($i = 0; $i -lt 8; $i++) {
            if ($resp[24 + $i] -ne $packet[40 + $i]) {
                return @{ Success = $false; Host = $hostName; Error = "Origin timestamp mismatch (possible spoofing)" }
            }
        }

        $t2 = ConvertFrom-NtpTimestamp $resp 32 # Receive timestamp
        $t3 = ConvertFrom-NtpTimestamp $resp 40 # Transmit timestamp

        if ($null -eq $t2 -or $null -eq $t3) {
            return @{ Success = $false; Host = $hostName; Error = "Server returned zero timestamp" }
        }

        # Standard RFC 4330 4-timestamp calculation:
        # Delay = (t4 - t1) - (t3 - t2)
        # Offset = ((t2 - t1) + (t3 - t4)) / 2
        $delayMs = (($t4 - $t1).TotalMilliseconds) - (($t3 - $t2).TotalMilliseconds)
        if ($delayMs -lt 0) { $delayMs = 0 }
        $offsetMs = ((($t2 - $t1).TotalMilliseconds) + (($t3 - $t4).TotalMilliseconds)) / 2.0

        $targetUtc = $t4.AddMilliseconds($offsetMs)

        return @{
            Success = $true
            Host = $hostName
            ResolvedIp = $endpoint.Address.ToString()
            UtcTime = $targetUtc
            OffsetMs = $offsetMs
            LatencyMs = [math]::Round($delayMs, 1)
            Stratum = $stratum
        }
    } catch {
        return @{ Success = $false; Host = $hostName; Error = $_.Exception.Message }
    } finally {
        if ($client) { $client.Close() }
    }
}

function Get-HttpsTimeFromHost($url, $name) {
    try {
        $req = [System.Net.HttpWebRequest]::Create($url)
        $req.Timeout = 4000
        $req.Method = "HEAD"
        $req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $res = $req.GetResponse()
        $sw.Stop()
        $httpDate = $res.Headers["Date"]
        $res.Close()
        if ($httpDate) {
            $utc = [datetime]::ParseExact($httpDate, "ddd, dd MMM yyyy HH:mm:ss GMT", [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::AssumeUniversal)
            return @{ Success = $true; Host = $name; UtcTime = $utc; LatencyMs = $sw.ElapsedMilliseconds }
        }
    } catch [System.Net.WebException] {
        if ($_.Exception.Status -eq [System.Net.WebExceptionStatus]::TrustFailure) {
            return @{ Success = $false; Host = $name; Error = "TLS Certificate trust failure (local clock may be severely skewed, e.g. RDR2 2019 preset)" }
        }
        return @{ Success = $false; Host = $name; Error = $_.Exception.Message }
    } catch {
        return @{ Success = $false; Host = $name; Error = $_.Exception.Message }
    }
    return @{ Success = $false; Host = $name; Error = "No Date header returned" }
}

function Sync-SystemPeers {
    Write-Host "  [*] Synchronizing with Windows System Configured Peers..." -ForegroundColor Cyan
    Write-Host ""

    # Ensure w32time service is running
    try {
        $svc = Get-Service w32time -ErrorAction SilentlyContinue
        if ($svc.Status -ne 'Running') {
            Write-Host "  [*] Starting Windows Time Service (w32time)..." -ForegroundColor Gray
            Start-Service w32time
            Start-Sleep -Milliseconds 800
        }
    } catch {
        Write-Host "  [!] Warning: Could not start w32time service." -ForegroundColor Red
    }

    # Dynamically read and show configured peers on this machine
    $peers = Get-ConfiguredPeers
    Write-Host "  Configured System Peers on this machine ($($peers.Count)):" -ForegroundColor White
    foreach ($p in $peers) {
        Write-Host "    - $($p.Host) (flags: $($p.Flag))" -ForegroundColor DarkCyan
    }
    Write-Host ""

    Write-Host "  [*] Executing: w32tm /resync /force ..." -ForegroundColor Gray
    $resyncOut = w32tm /resync /force 2>&1 | Out-String

    if ($LASTEXITCODE -eq 0 -and $resyncOut -match "command completed successfully") {
        Write-Host "  [OK] Successfully synchronized via Windows Time service (w32tm)!" -ForegroundColor Green
    } else {
        Write-Host "  [!] w32tm resync returned failure or notice." -ForegroundColor Yellow
        Write-Host "      Details: $($resyncOut.Trim())" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "  [*] Querying configured registry peers directly via SNTP..." -ForegroundColor Gray
        
        $results = @()
        foreach ($p in $peers) {
            Write-Host "    Querying $($p.Host) ... " -NoNewline -ForegroundColor Gray
            $res = Get-NtpTimeFromHost $p.Host
            if ($res.Success) {
                Write-Host "OK (Delay: $($res.LatencyMs)ms, Offset: $([math]::Round($res.OffsetMs, 1))ms, Stratum: $($res.Stratum))" -ForegroundColor Green
                $results += $res
            } else {
                Write-Host "Unreachable ($($res.Error))" -ForegroundColor DarkRed
            }
        }

        if ($results.Count -gt 0) {
            $sorted = $results | Sort-Object { $_.OffsetMs }
            $median = $sorted[[math]::Floor($sorted.Count / 2)]
            Write-Host ""
            Write-Host "  [*] Applying median offset ($([math]::Round($median.OffsetMs, 1))ms) from $($results.Count) responsive peer(s)..." -ForegroundColor Cyan
            try {
                Set-Date -Date ($median.UtcTime.ToLocalTime()) -ErrorAction Stop
                Write-Host "  [OK] System clock successfully synchronized from configured peers!" -ForegroundColor Green
            } catch {
                Write-Host "  [X] Failed to set system date: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host "  [X] None of the system peers responded via NTP (UDP port 123 may be blocked)." -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  Current Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss") -ForegroundColor Yellow
}

function Sync-GlobalTime {
    Write-Host "  [*] Synchronizing with Global International NTP Consensus..." -ForegroundColor Cyan
    Write-Host ""

    $globalNtp = @(
        "time.cloudflare.com",
        "time.google.com",
        "pool.ntp.org",
        "time.windows.com",
        "time.aws.com"
    )

    $results = @()
    foreach ($srv in $globalNtp) {
        Write-Host "  [*] Querying NTP: $srv ... " -NoNewline -ForegroundColor Gray
        $res = Get-NtpTimeFromHost $srv
        if ($res.Success) {
            Write-Host "OK (Delay: $($res.LatencyMs)ms, Offset: $([math]::Round($res.OffsetMs, 1))ms, Stratum: $($res.Stratum))" -ForegroundColor Green
            $results += $res
        } else {
            Write-Host "Failed ($($res.Error))" -ForegroundColor DarkGray
        }
    }

    $synced = $false
    if ($results.Count -gt 0) {
        $sorted = $results | Sort-Object { $_.OffsetMs }
        $medianRes = $sorted[[math]::Floor($sorted.Count / 2)]

        # Outlier rejection (> 5000ms deviation from median)
        $validSources = $sorted | Where-Object { [math]::Abs($_.OffsetMs - $medianRes.OffsetMs) -le 5000 }
        
        Write-Host ""
        Write-Host "  [*] Applying consensus time from $($validSources.Count)/$($results.Count) responsive servers (Median Offset: $([math]::Round($medianRes.OffsetMs, 1))ms)..." -ForegroundColor Cyan

        try {
            $targetLocal = $medianRes.UtcTime.ToLocalTime()
            Set-Date -Date $targetLocal -ErrorAction Stop
            Write-Host "  [OK] Successfully synchronized clock from Global NTP consensus!" -ForegroundColor Green
            $synced = $true
        } catch {
            Write-Host "  [X] Error applying system date: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    # Fallback to HTTPS Date header (port 443)
    if (-not $synced) {
        Write-Host ""
        Write-Host "  [!] UDP NTP packets timed out or blocked. Trying Secure HTTPS Date header (Port 443, ~1s precision)..." -ForegroundColor Yellow
        $httpTargets = @(
            @{ Url = "https://www.google.com"; Name = "Google HTTPS (Global)" },
            @{ Url = "https://cloudflare.com"; Name = "Cloudflare HTTPS (Global)" },
            @{ Url = "https://www.microsoft.com"; Name = "Microsoft Global HTTPS" }
        )

        foreach ($tgt in $httpTargets) {
            Write-Host "  [*] Connecting: $($tgt.Name) ... " -NoNewline -ForegroundColor Gray
            $res = Get-HttpsTimeFromHost $tgt.Url $tgt.Name
            if ($res.Success) {
                Write-Host "OK ($($res.LatencyMs)ms)" -ForegroundColor Green
                try {
                    Set-Date -Date ($res.UtcTime.ToLocalTime()) -ErrorAction Stop
                    Write-Host "  [OK] Successfully synchronized from: $($tgt.Name) (coarse 1-second precision)" -ForegroundColor Green
                    $synced = $true
                    break
                } catch {
                    Write-Host "  [X] Error applying system date: $($_.Exception.Message)" -ForegroundColor Red
                }
            } else {
                Write-Host "Failed ($($res.Error))" -ForegroundColor DarkGray
            }
        }
    }

    if (-not $synced) {
        Write-Host "  [X] Failed to connect to any international servers. Check your internet connection and firewall." -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "  Current Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss") -ForegroundColor Yellow
}

function Set-CustomTime {
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "                      SET CUSTOM DATE AND TIME" -ForegroundColor Yellow
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "  [1] Enter Custom Year, Month, Day, Hour, Minute" -ForegroundColor White
    Write-Host "  [2] Quick Reset to Today / Real-Time Now" -ForegroundColor Green
    Write-Host "  [0] Cancel / Back" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Select option [0-2]: " -NoNewline -ForegroundColor Yellow
    $mode = Read-Host

    if ($mode -eq "2") {
        Write-Host "  [*] Querying system RTC / syncing current real-time..." -ForegroundColor Gray
        Sync-SystemPeers
        return
    }
    if ($mode -ne "1") {
        Write-Host "  Cancelled." -ForegroundColor DarkGray
        return
    }

    Write-Host ""
    Write-Host "  Press [Enter] on any field to keep the current value." -ForegroundColor DarkGray
    Write-Host ""

    $curr = Get-Date
    
    # Year
    $defY = $curr.Year
    Write-Host "  Enter Year   (Default: $defY): " -NoNewline -ForegroundColor White
    $y = Read-Host
    if ([string]::IsNullOrWhiteSpace($y)) { $y = $defY }
    
    # Month
    $defM = $curr.Month
    Write-Host "  Enter Month  (1-12, Default: $defM): " -NoNewline -ForegroundColor White
    $m = Read-Host
    if ([string]::IsNullOrWhiteSpace($m)) { $m = $defM }

    # Day
    $defD = $curr.Day
    Write-Host "  Enter Day    (1-31, Default: $defD): " -NoNewline -ForegroundColor White
    $d = Read-Host
    if ([string]::IsNullOrWhiteSpace($d)) { $d = $defD }

    # Hour
    $defH = $curr.Hour
    Write-Host "  Enter Hour   (0-23, Default: $defH): " -NoNewline -ForegroundColor White
    $h = Read-Host
    if ([string]::IsNullOrWhiteSpace($h)) { $h = $defH }

    # Minute
    $defMin = $curr.Minute
    Write-Host "  Enter Minute (0-59, Default: $defMin): " -NoNewline -ForegroundColor White
    $min = Read-Host
    if ([string]::IsNullOrWhiteSpace($min)) { $min = $defMin }

    $targetStr = "$y-$m-$d $h`:$min`:00"
    Write-Host ""

    $parsed = $null
    try {
        $parsed = [datetime]::Parse($targetStr)
    } catch {
        Write-Host "  [X] Invalid date format: $targetStr" -ForegroundColor Red
        return
    }

    if ([math]::Abs(($parsed - (Get-Date)).TotalHours) -gt 24) {
        Write-Host "  [!] WARNING: Target date/time is more than 24 hours away from current time." -ForegroundColor Yellow
        Write-Host "      Large clock adjustments will disrupt TLS/SSL certificates and active logins." -ForegroundColor Yellow
        Write-Host "  Continue with adjustment? (y/N): " -NoNewline -ForegroundColor White
        $c = Read-Host
        if ($c -ne "y" -and $c -ne "Y") {
            Write-Host "  Operation canceled." -ForegroundColor DarkGray
            return
        }
    }

    Write-Host "  Applying: $targetStr ..." -ForegroundColor Gray
    try {
        Set-Date -Date $parsed -ErrorAction Stop
        $verified = Get-Date
        if ([math]::Abs(($verified - $parsed).TotalSeconds) -lt 10) {
            Write-Host "  [OK] System date and time successfully updated and verified!" -ForegroundColor Green
        } else {
            Write-Host "  [X] Verification failed: Time did not match target." -ForegroundColor Red
        }
    } catch {
        Write-Host "  [X] Failed to set time: $($_.Exception.Message). Ensure script runs with Administrator privileges." -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "  Current Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss") -ForegroundColor Yellow
}

function Set-Rdr2Preset {
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "                      RDR2 LAUNCH WORKAROUND" -ForegroundColor Yellow
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "  [!] WARNING: Setting system clock to 2019-10-15 will disrupt HTTPS/TLS" -ForegroundColor Yellow
    Write-Host "      certificates, active browser logins, and secure network connections." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Are you sure you want to apply RDR2 2019 time? (y/N): " -NoNewline -ForegroundColor White
    $confirm = Read-Host
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Host "  Operation canceled." -ForegroundColor DarkGray
        return
    }

    Write-Host "  [*] Setting fixed time for RDR2 Preset (2019-10-15 21:31:00)..." -ForegroundColor Cyan
    try {
        $target = [datetime]"2019-10-15 21:31:00"
        Set-Date -Date $target -ErrorAction Stop
        $curr = Get-Date
        if ($curr.Year -eq 2019 -and $curr.Month -eq 10) {
            Write-Host "  [OK] Done! Time verified set to $($curr.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Green
        } else {
            Write-Host "  [X] Failed: Clock was not updated. Ensure Administrator privileges." -ForegroundColor Red
        }
    } catch {
        Write-Host "  [X] Failed to set time: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "  Current Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss") -ForegroundColor Yellow
}

function View-Status {
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "                     SYSTEM PEERS AND STATUS" -ForegroundColor Yellow
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host ""

    # Windows Time Service
    $svc = Get-Service w32time -ErrorAction SilentlyContinue
    $svcStatus = if ($svc) { $svc.Status } else { "Not Installed" }
    Write-Host "  Windows Time Service (w32time): " -NoNewline -ForegroundColor Gray
    if ($svcStatus -eq 'Running') {
        Write-Host "$svcStatus" -ForegroundColor Green
    } else {
        Write-Host "$svcStatus" -ForegroundColor Red
    }

    # Dynamically read configured peers from registry
    $peers = Get-ConfiguredPeers
    Write-Host ""
    Write-Host "  Configured Peers on this System ($($peers.Count) Total):" -ForegroundColor White
    $idx = 1
    foreach ($p in $peers) {
        Write-Host "    [$idx] " -NoNewline -ForegroundColor Cyan
        Write-Host "$($p.Host)" -NoNewline -ForegroundColor Yellow
        Write-Host " (flags: $($p.Flag))" -NoNewline -ForegroundColor DarkGray
        
        # Fast NTP latency test
        Write-Host " -> " -NoNewline -ForegroundColor DarkGray
        $testRes = Get-NtpTimeFromHost $p.Host
        if ($testRes.Success) {
            Write-Host "Online (Delay: $($testRes.LatencyMs)ms, Stratum: $($testRes.Stratum))" -ForegroundColor Green
        } else {
            Write-Host "Unreachable (UDP 123 offline/filtered)" -ForegroundColor DarkRed
        }
        $idx++
    }

    Write-Host ""
    Write-Host "  Local System Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss (dddd)") -ForegroundColor Yellow
    Write-Host "  UTC Time:          " -NoNewline -ForegroundColor Gray
    Write-Host ([datetime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss")) -ForegroundColor Gray
    Write-Host "  Time Zone:         " -NoNewline -ForegroundColor Gray
    Write-Host ([System.TimeZoneInfo]::Local.DisplayName) -ForegroundColor DarkCyan
}

function Add-CustomPeer {
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "                    ADD CUSTOM NTP SERVER" -ForegroundColor Yellow
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Enter server hostname or IP address (e.g. time.google.com or 129.6.15.28):" -ForegroundColor White
    Write-Host "  > " -NoNewline -ForegroundColor Yellow
    $raw = (Read-Host).Trim()
    
    $check = Test-PeerAddress $raw
    if (-not $check.IsValid) {
        Write-Host "  [X] VALIDATION ERROR: $($check.Error)" -ForegroundColor Red
        return
    }
    $cleanHost = $check.CleanHost

    # Check if already in list
    $existing = Get-ConfiguredPeers
    foreach ($p in $existing) {
        if ($p.Host.Equals($cleanHost, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "  [!] Server '$cleanHost' is already configured in your system peer list." -ForegroundColor Yellow
            return
        }
    }

    # Live DNS Resolution Check
    Write-Host "  [*] Resolving DNS for $cleanHost ... " -NoNewline -ForegroundColor Gray
    try {
        $addrs = [System.Net.Dns]::GetHostAddresses($cleanHost)
        if (-not $addrs -or $addrs.Count -eq 0) {
            Write-Host "FAILED" -ForegroundColor Red
            Write-Host "  [X] DNS Error: No IP address found for '$cleanHost'." -ForegroundColor Red
            return
        }
        $ipStr = $addrs[0].IPAddressToString
        Write-Host "OK ($ipStr)" -ForegroundColor Green
    } catch {
        Write-Host "FAILED" -ForegroundColor Red
        Write-Host "  [X] DNS Resolution Failed: Host '$cleanHost' could not be resolved." -ForegroundColor Red
        return
    }

    # Live NTP Latency Test
    Write-Host "  [*] Testing NTP connectivity (UDP 123) ... " -NoNewline -ForegroundColor Gray
    $res = Get-NtpTimeFromHost $cleanHost
    if ($res.Success) {
        Write-Host "OK (Delay: $($res.LatencyMs)ms, Stratum: $($res.Stratum))" -ForegroundColor Green
    } else {
        Write-Host "Timeout / Filtered" -ForegroundColor Yellow
        Write-Host "      (Notice: $($res.Error) - server can still be added to registry)" -ForegroundColor DarkGray
    }

    # Add to list
    $newList = @()
    foreach ($p in $existing) { $newList += $p }
    $newList += [PSCustomObject]@{ Host = $cleanHost; Flag = "0x8"; Raw = "$cleanHost,0x8" }
    
    if (Set-ConfiguredPeers $newList) {
        Write-Host ""
        Write-Host "  [OK] SUCCESS: Server '$cleanHost' successfully added to Windows Time Service!" -ForegroundColor Green
    }
}

function Remove-Peer {
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "                    REMOVE NTP SERVER" -ForegroundColor Yellow
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host ""

    $peers = Get-ConfiguredPeers
    if ($peers.Count -eq 0) {
        Write-Host "  [!] No peers found to remove." -ForegroundColor Yellow
        return
    }

    if ($peers.Count -le 1) {
        Write-Host "  [X] Action Denied: Only 1 peer configured ($($peers[0].Host))." -ForegroundColor Red
        Write-Host "      Windows Time service requires at least one configured NTP peer." -ForegroundColor Yellow
        return
    }

    Write-Host "  Configured NTP Peers:" -ForegroundColor White
    for ($i = 0; $i -lt $peers.Count; $i++) {
        $num = $i + 1
        Write-Host "   [$num] " -NoNewline -ForegroundColor Cyan
        Write-Host "$($peers[$i].Host)" -ForegroundColor Yellow
    }
    Write-Host "   [0] Cancel" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Enter peer number to remove [1-$($peers.Count)]: " -NoNewline -ForegroundColor White
    $choice = Read-Host

    if ($choice -eq "0" -or [string]::IsNullOrWhiteSpace($choice)) {
        Write-Host "  Cancelled." -ForegroundColor DarkGray
        return
    }

    $idx = 0
    if ([int]::TryParse($choice, [ref]$idx) -and $idx -ge 1 -and $idx -le $peers.Count) {
        $target = $peers[$idx - 1]
        $newList = @()
        for ($i = 0; $i -lt $peers.Count; $i++) {
            if ($i -ne ($idx - 1)) {
                $newList += $peers[$i]
            }
        }

        Write-Host "  [*] Removing peer '$($target.Host)' ..." -ForegroundColor Gray
        if (Set-ConfiguredPeers $newList) {
            Write-Host "  [OK] SUCCESS: Removed peer '$($target.Host)' from Windows Time Service!" -ForegroundColor Green
        }
    } else {
        Write-Host "  [X] Invalid selection." -ForegroundColor Red
    }
}

function Apply-PresetPeers($presetType) {
    $selectedPeers = ""
    switch ($presetType) {
        "iran" {
            $selectedPeers = "time.windows.com,0x8 pool.ntp.org,0x8 time.cloudflare.com,0x8 time.digiboy.ir,0x8 ntp.iranet.ir,0x8"
            Write-Host "  Applying Iran-Optimized 5 peers..." -ForegroundColor Gray
        }
        "global" {
            $selectedPeers = "time.windows.com,0x8 pool.ntp.org,0x8 time.cloudflare.com,0x8"
            Write-Host "  Applying Global Tier-1 3 peers..." -ForegroundColor Gray
        }
        "default" {
            $selectedPeers = "time.windows.com,0x8"
            Write-Host "  Resetting to Windows Default (time.windows.com)..." -ForegroundColor Gray
        }
    }

    # Check domain join status
    try {
        $comp = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
        if ($comp -and $comp.PartOfDomain) {
            Write-Host "  [!] Notice: Machine is joined to domain '$($comp.Domain)'. Active Directory Group Policy may override manual peers." -ForegroundColor Yellow
        }
    } catch {}

    try {
        # Never specify /reliable:yes on client workstations
        $w32Out = w32tm /config /manualpeerlist:"$selectedPeers" /syncfromflags:manual /update 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0 -and -not ($w32Out -match "command completed successfully")) {
            Write-Host "  [X] Error configuring peers via w32tm: $($w32Out.Trim())" -ForegroundColor Red
            return
        }
        $null = sc.exe config w32time start= auto 2>&1
        Restart-Service w32time -Force -ErrorAction SilentlyContinue
        Write-Host "  [OK] Successfully configured and applied peers to Windows Time Service!" -ForegroundColor Green
    } catch {
        Write-Host "  [X] Error applying peers: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Manage-Peers-Menu {
    while ($true) {
        Show-Header
        Write-Host "  Manage NTP Peers:" -ForegroundColor White
        Write-Host "   [1]  View Configured Peers & Live Test" -ForegroundColor Cyan
        Write-Host "   [2]  Add Custom NTP Server (with Live DNS & NTP Pre-Test)" -ForegroundColor Green
        Write-Host "   [3]  Remove an NTP Server" -ForegroundColor Red
        Write-Host "   [4]  Preset: Iran-Optimized (Windows, Pool, Cloudflare, Digiboy, Iranet)" -ForegroundColor DarkCyan
        Write-Host "   [5]  Preset: Global Tier-1 (Windows, Pool, Cloudflare)" -ForegroundColor DarkCyan
        Write-Host "   [6]  Preset: Reset to Windows Default (time.windows.com)" -ForegroundColor DarkGray
        Write-Host "   [0]  Back to Main Menu" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  Enter option [0-6]: " -NoNewline -ForegroundColor Yellow
        $sub = Read-Host
        Write-Host ""

        switch ($sub) {
            "1" { View-Status }
            "2" { Add-CustomPeer }
            "3" { Remove-Peer }
            "4" { Apply-PresetPeers "iran" }
            "5" { Apply-PresetPeers "global" }
            "6" { Apply-PresetPeers "default" }
            "0" { return }
            Default { Write-Host "  [!] Invalid selection." -ForegroundColor Red }
        }

        Write-Host ""
        Write-Host "  Press any key to continue..." -ForegroundColor DarkGray
        try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
    }
}

function Repair-W32Time {
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "               REPAIR WINDOWS TIME SERVICE (W32TIME)" -ForegroundColor Yellow
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Backup current peers so repair NEVER deletes user configured peers
    $savedPeers = Get-ConfiguredPeers
    Write-Host "  [*] Backing up current configured peers ($($savedPeers.Count) peers)..." -ForegroundColor Gray
    
    Write-Host "  [*] Stopping w32time service..." -ForegroundColor Gray
    Stop-Service w32time -Force -ErrorAction SilentlyContinue
    
    Write-Host "  [*] Re-registering w32tm service in Windows registry..." -ForegroundColor Gray
    $null = w32tm /unregister 2>&1
    Start-Sleep -Milliseconds 500
    $null = w32tm /register 2>&1
    Start-Sleep -Milliseconds 500
    $null = sc.exe config w32time start= auto 2>&1
    
    Write-Host "  [*] Starting w32time service..." -ForegroundColor Gray
    Start-Service w32time -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800

    # Restore saved peers
    if ($savedPeers -and $savedPeers.Count -gt 0) {
        Write-Host "  [*] Restoring your configured peers..." -ForegroundColor Gray
        $null = Set-ConfiguredPeers $savedPeers
    }
    
    Write-Host "  [*] Forcing resync: w32tm /resync /force ..." -ForegroundColor Gray
    $out = w32tm /resync /force 2>&1 | Out-String
    Write-Host "      $($out.Trim())" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  [OK] Windows Time service repair completed! (Peers preserved)" -ForegroundColor Green
}

# ---- Main Interactive Loop ----
while ($true) {
    Show-Header
    Write-Host "  Main Actions:" -ForegroundColor White
    Write-Host "   [1]  Sync with Windows System Peers (w32tm auto config)" -ForegroundColor Cyan
    Write-Host "   [2]  Sync with Global NTP (Consensus + HTTPS fallback)" -ForegroundColor Cyan
    Write-Host "   [3]  Set Custom Date and Time (interactive / quick today)" -ForegroundColor Cyan
    Write-Host "   [4]  View System Peers and NTP Status (Live Test)" -ForegroundColor Cyan
    Write-Host "   [5]  Manage NTP Peers (Add, Remove, Test, Presets)" -ForegroundColor Yellow
    Write-Host "   [6]  Diagnostics & Repair Windows Time Service" -ForegroundColor DarkCyan
    Write-Host ""
    Write-Host "  Game Presets / Workarounds:" -ForegroundColor White
    Write-Host "   [7]  Set RDR2 Game Fix Date (2019-10-15 21:31)" -ForegroundColor DarkYellow
    Write-Host ""
    Write-Host "   [0]  Exit" -ForegroundColor Red

    Write-Host ""
    Write-Host "  Enter option [0-7]: " -NoNewline -ForegroundColor Yellow
    $choice = Read-Host
    Write-Host ""

    switch ($choice) {
        "1" {
            Clear-Host
            Show-Header
            Sync-SystemPeers
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "2" {
            Clear-Host
            Show-Header
            Sync-GlobalTime
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "3" {
            Clear-Host
            Show-Header
            Set-CustomTime
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "4" {
            Clear-Host
            Show-Header
            View-Status
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "5" {
            Manage-Peers-Menu
        }
        "6" {
            Clear-Host
            Show-Header
            Repair-W32Time
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "7" {
            Clear-Host
            Show-Header
            Set-Rdr2Preset
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "0" {
            Write-Host "  Goodbye!" -ForegroundColor Cyan
            Start-Sleep -Milliseconds 500
            exit
        }
        Default {
            Write-Host "  [!] Invalid choice. Please choose 0 to 7." -ForegroundColor Red
            Start-Sleep -Seconds 1
        }
    }
}
