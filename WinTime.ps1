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
        Write-Host "  [X] Error: Peer list cannot be empty. Windows requires at least one peer." -ForegroundColor Red
        return $false
    }
    $valStr = ($peerObjects | ForEach-Object { "$($_.Host),$($_.Flag)" }) -join " "
    try {
        # Use official Microsoft w32tm administrative tool instead of direct registry manipulation
        $null = w32tm /config /manualpeerlist:"$valStr" /syncfromflags:manual /reliable:yes /update 2>&1
        $null = sc.exe config w32time start= auto 2>&1
        Restart-Service w32time -Force -ErrorAction SilentlyContinue
        return $true
    } catch {
        Write-Host "  [X] Error updating Windows Time Service: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Show-Header {
    Clear-Host
    $now = Get-Date -Format "yyyy-MM-dd  HH:mm:ss"
    $tz = [System.TimeZoneInfo]::Local.DisplayName
    
    $peers = Get-ConfiguredPeers
    $peerCount = $peers.Count

    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "         WINTIME v0.1.0 (ALPHA) - WINDOWS TIME & NTP MANAGER" -ForegroundColor Yellow
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

function Get-NtpTimeFromHost($hostName) {
    try {
        $client = New-Object System.Net.Sockets.UdpClient
        $client.Client.ReceiveTimeout = 2500
        $client.Connect($hostName, 123)
        $data = New-Object byte[] 48
        $data[0] = 0x1B # NTP v3 Client
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        [void]$client.Send($data, $data.Length)
        $endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        $resp = $client.Receive([ref]$endpoint)
        $sw.Stop()
        $client.Close()
        if ($resp.Length -ge 48) {
            $intPart = [uint32](([uint32]$resp[40] -shl 24) -bor ([uint32]$resp[41] -shl 16) -bor ([uint32]$resp[42] -shl 8) -bor [uint32]$resp[43])
            $epoch = [datetime]::SpecifyKind([datetime]"1900-01-01 00:00:00", [System.DateTimeKind]::Utc)
            $utc = $epoch.AddSeconds($intPart)
            return @{ Success = $true; Host = $hostName; UtcTime = $utc; LatencyMs = $sw.ElapsedMilliseconds }
        }
    } catch {}
    return @{ Success = $false; Host = $hostName; Error = "Timeout or Unreachable" }
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
    } catch {}
    return @{ Success = $false; Host = $name; Error = "Failed to connect" }
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

    if ($resyncOut -match "command completed successfully" -or $LASTEXITCODE -eq 0) {
        Write-Host "  [OK] Successfully synchronized via Windows Time service!" -ForegroundColor Green
    } else {
        Write-Host "  [!] w32tm resync did not succeed." -ForegroundColor Yellow
        Write-Host "      Details: $($resyncOut.Trim())" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "  [*] Querying configured peers directly via NTP..." -ForegroundColor Gray
        
        $synced = $false
        foreach ($p in $peers) {
            Write-Host "    Querying $($p.Host) ... " -NoNewline -ForegroundColor Gray
            $res = Get-NtpTimeFromHost $p.Host
            if ($res.Success) {
                Write-Host "OK ($($res.LatencyMs)ms)" -ForegroundColor Green
                try {
                    $null = Set-Date -Date ($res.UtcTime.ToLocalTime()) 2>&1
                    Write-Host "  [OK] Clock successfully updated from peer: $($p.Host)" -ForegroundColor Green
                    $synced = $true
                    break
                } catch {
                    Write-Host "  [X] Failed to set date: $($_.Exception.Message)" -ForegroundColor Red
                }
            } else {
                Write-Host "Unreachable" -ForegroundColor DarkRed
            }
        }
        if (-not $synced) {
            Write-Host "  [X] None of the system peers responded via NTP (UDP port 123 may be blocked)." -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  Current Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss") -ForegroundColor Yellow
}

function Sync-GlobalTime {
    Write-Host "  [*] Synchronizing with Global International NTP (No Iranian Servers)..." -ForegroundColor Cyan
    Write-Host ""

    $globalNtp = @(
        "time.cloudflare.com",
        "time.google.com",
        "pool.ntp.org",
        "time.windows.com",
        "time.aws.com"
    )

    $synced = $false

    # Query Tier-1 Global NTP
    foreach ($srv in $globalNtp) {
        Write-Host "  [*] Querying NTP: $srv ... " -NoNewline -ForegroundColor Gray
        $res = Get-NtpTimeFromHost $srv
        if ($res.Success) {
            Write-Host "OK ($($res.LatencyMs)ms)" -ForegroundColor Green
            try {
                $null = Set-Date -Date ($res.UtcTime.ToLocalTime()) 2>&1
                Write-Host "  [OK] Successfully synchronized from: $srv (Tier-1 NTP)" -ForegroundColor Green
                $synced = $true
                break
            } catch {
                Write-Host "  [X] Error applying system date: $($_.Exception.Message)" -ForegroundColor Red
            }
        } else {
            Write-Host "Unreachable" -ForegroundColor DarkGray
        }
    }

    # Fallback to HTTPS Atomic Time (port 443 - never blocked by firewall/ISP)
    if (-not $synced) {
        Write-Host ""
        Write-Host "  [!] UDP NTP packets timed out. Trying Secure HTTPS Global Time (Port 443)..." -ForegroundColor Yellow
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
                    $null = Set-Date -Date ($res.UtcTime.ToLocalTime()) 2>&1
                    Write-Host "  [OK] Successfully synchronized from: $($tgt.Name)" -ForegroundColor Green
                    $synced = $true
                    break
                } catch {
                    Write-Host "  [X] Error applying system date: $($_.Exception.Message)" -ForegroundColor Red
                }
            } else {
                Write-Host "Failed" -ForegroundColor DarkGray
            }
        }
    }

    if (-not $synced) {
        Write-Host "  [X] Failed to connect to any international servers. Check your internet connection." -ForegroundColor Red
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
    Write-Host "  Applying: $targetStr ..." -ForegroundColor Gray

    $done = $false
    try {
        $parsed = [datetime]::Parse($targetStr)
        Set-Date -Date $parsed -ErrorAction Stop
        $done = $true
    } catch {
        $done = $false
    }
    if ($done) {
        Write-Host "  [OK] System date and time successfully updated!" -ForegroundColor Green
    } else {
        Write-Host "  [X] Failed to set time. Ensure script is running with Administrator privileges." -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "  Current Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss") -ForegroundColor Yellow
}

function Set-Rdr2Preset {
    Write-Host "  [*] Setting fixed time for RDR2 Preset (2019-10-15 21:31:00)..." -ForegroundColor Cyan
    Write-Host ""
    $done = $false
    try {
        Set-Date -Date "2019-10-15 21:31:00" -ErrorAction Stop
        $done = $true
    } catch {
        $done = $false
    }
    if ($done) {
        Write-Host "  [OK] Done! Time set to 2019-10-15 21:31:00" -ForegroundColor Green
    } else {
        Write-Host "  [X] Failed to set time. Please make sure script is running as Administrator." -ForegroundColor Red
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
            Write-Host "Online ($($testRes.LatencyMs)ms)" -ForegroundColor Green
        } else {
            Write-Host "Unreachable (UDP 123 offline/filtered)" -ForegroundColor DarkRed
        }
        $idx++
    }

    Write-Host ""
    Write-Host "  Local System Time: " -NoNewline -ForegroundColor Gray
    Write-Host (Get-Date -Format "yyyy-MM-dd HH:mm:ss (dddd)") -ForegroundColor Yellow
    Write-Host "  UTC Atomic Time:   " -NoNewline -ForegroundColor Gray
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
    
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Write-Host "  [X] Error: Server address cannot be empty." -ForegroundColor Red
        return
    }

    # Strict Validation: English characters only, no Persian, no spaces
    foreach ($ch in $raw.ToCharArray()) {
        if ([int]$ch -gt 127 -or [char]::IsWhiteSpace($ch) -or $ch -in @(',', ';', '/', '\', '?', '*', '!')) {
            Write-Host "  [X] VALIDATION ERROR: Server address contains invalid characters." -ForegroundColor Red
            Write-Host "      Only English letters, numbers, hyphens, and dots are allowed." -ForegroundColor DarkGray
            return
        }
    }

    # Format Check
    $isIp = [System.Net.IPAddress]::TryParse($raw, [ref]$null)
    $isDomain = $raw.Contains(".") -and -not $raw.StartsWith(".") -and -not $raw.EndsWith(".") -and -not $raw.Contains("..")
    if (-not $isIp -and -not $isDomain -and -not ($raw -eq "localhost")) {
        Write-Host "  [X] VALIDATION ERROR: '$raw' is not a valid domain or IP address." -ForegroundColor Red
        return
    }

    # Check if already in list
    $existing = Get-ConfiguredPeers
    foreach ($p in $existing) {
        if ($p.Host.Equals($raw, [StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "  [!] Server '$raw' is already configured in your system peer list." -ForegroundColor Yellow
            return
        }
    }

    # Live DNS Resolution Check
    Write-Host "  [*] Resolving DNS for $raw ... " -NoNewline -ForegroundColor Gray
    try {
        $addrs = [System.Net.Dns]::GetHostAddresses($raw)
        if (-not $addrs -or $addrs.Count -eq 0) {
            Write-Host "FAILED" -ForegroundColor Red
            Write-Host "  [X] DNS Error: No IP address found for '$raw'." -ForegroundColor Red
            return
        }
        $ipStr = $addrs[0].IPAddressToString
        Write-Host "OK ($ipStr)" -ForegroundColor Green
    } catch {
        Write-Host "FAILED" -ForegroundColor Red
        Write-Host "  [X] DNS Resolution Failed: Host '$raw' does not exist." -ForegroundColor Red
        return
    }

    # Live NTP Latency Test
    Write-Host "  [*] Testing NTP connectivity (UDP 123) ... " -NoNewline -ForegroundColor Gray
    $res = Get-NtpTimeFromHost $raw
    if ($res.Success) {
        Write-Host "OK ($($res.LatencyMs)ms)" -ForegroundColor Green
    } else {
        Write-Host "Timeout" -ForegroundColor Yellow
        Write-Host "      (Notice: DNS resolved, but UDP 123 timed out - ISP or firewall may throttle UDP)" -ForegroundColor DarkGray
    }

    # Add to list
    $newList = @()
    foreach ($p in $existing) { $newList += $p }
    $newList += [PSCustomObject]@{ Host = $raw; Flag = "0x8"; Raw = "$raw,0x8" }
    
    if (Set-ConfiguredPeers $newList) {
        Write-Host ""
        Write-Host "  [OK] SUCCESS: Server '$raw' successfully added to Windows Time Service!" -ForegroundColor Green
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
        Write-Host "      Windows requires at least one active NTP peer." -ForegroundColor Yellow
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

    try {
        # Use official Microsoft w32tm administrative tool instead of direct registry manipulation
        $null = w32tm /config /manualpeerlist:"$selectedPeers" /syncfromflags:manual /reliable:yes /update 2>&1
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
    Write-Host "  Options:" -ForegroundColor White
    Write-Host "   [1]  Sync with Windows System Peers (w32tm auto config)" -ForegroundColor Cyan
    Write-Host "   [2]  Sync with Global NTP (International only, no .ir)" -ForegroundColor Cyan
    Write-Host "   [3]  Set Custom Date and Time (interactive / quick today)" -ForegroundColor Cyan
    Write-Host "   [4]  Set Fixed Date for RDR2 (2019-10-15 21:31)" -ForegroundColor Cyan
    Write-Host "   [5]  View System Peers and NTP Status (Live Test)" -ForegroundColor Cyan
    Write-Host "   [6]  Manage NTP Peers (Add, Remove, Test, Presets)" -ForegroundColor Yellow
    Write-Host "   [7]  Diagnostics & Repair Windows Time Service" -ForegroundColor DarkCyan
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
            Set-Rdr2Preset
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "5" {
            Clear-Host
            Show-Header
            View-Status
            Write-Host ""
            Write-Host "  Press any key to return to menu..." -ForegroundColor DarkGray
            try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch { [void][Console]::ReadLine() }
        }
        "6" {
            Manage-Peers-Menu
        }
        "7" {
            Clear-Host
            Show-Header
            Repair-W32Time
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
