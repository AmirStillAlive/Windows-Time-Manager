
# ⚡ WinTime — Windows Time & NTP Manager

<p align="center">
  <img src="assets/logo.png" width="160" height="160" alt="WinTime Logo" />
</p>

<p align="center">
  <a href="README.md"><b>English</b></a> | <a href="README.fa.md"><b>فارسی</b></a>
</p>

> A lightweight and fast Windows utility for system clock synchronization, NTP peer management, custom time presets, and game-specific time presets such as **Red Dead Redemption 2**.

[![Build](https://github.com/AmirStillAlive/Windows-Time-Manager/actions/workflows/build.yml/badge.svg)](https://github.com/AmirStillAlive/Windows-Time-Manager/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue)](https://www.microsoft.com/windows)
[![Language](https://img.shields.io/badge/language-C%23-purple)](#)


> ⚠️ **WinTime is currently in alpha. Expect bugs, breaking changes, and incomplete features. No guarantees are provided at this stage.**
---

## Why WinTime?

* **🚀 Compact standalone executable (~155 KB):** Compiled directly with native Windows components without bundling third-party runtimes, Electron, or large framework dependencies.

* **⚡ Fast startup & minimal footprint:** Instant launch with negligible RAM and CPU overhead.

* **🎯 RFC 4330 / 5905 Protocol Compliance:** Implements complete 4-timestamp SNTP calculations, network delay calculation, origin nonce verification, and Stratum validation.

* **🌐 Multi-Source NTP Consensus:** Queries multiple independent time sources concurrently, eliminates outliers, and selects the median offset for reliable synchronization.

* **✨ Modern Windows UI:** A clean dark interface with custom vector icons and DPI-aware layout for multi-monitor display scaling.

* **🛡️ Safe Service & Peer Management:** Strict semantic validation for IPv4, IPv6, and FQDN peer entries; domain join detection; and elimination of false-success process reporting.

---

## Features

### 1. 🎮 Game Time Presets (RDR2 Workaround)

* Sets the system clock to the predefined `2019-10-15 21:31:00` timestamp used by the RDR2 launch workaround.
* Requests explicit user confirmation before applying large clock adjustments (> 24h) to prevent unexpected TLS certificate disruptions.
* Uses Windows `SeSystemtimePrivilege` elevation with sub-second millisecond precision via `SetSystemTime`.

### 2. 🔄 High-Precision System Time Synchronization

* **Windows Time Service (`w32tm`):** Resynchronizes the system using registered Windows Time peers with `w32tm /resync /force`.
* **Direct SNTP Multi-Source Consensus:** If the Windows Time service resync fails (e.g. ISP packet inspection or domain policies), WinTime queries multiple Stratum 1/2 servers (`time.cloudflare.com`, `time.google.com`, `pool.ntp.org`, `time.windows.com`), computes round-trip delay and offset using standard 4-timestamp math, and applies the median offset.
* **HTTPS Date Header Fallback:** When UDP port 123 is completely blocked by restrictive firewalls or ISPs, WinTime falls back to retrieving atomic HTTP Date headers over port 443 with built-in TLS clock skew diagnostics.

### 3. ⚙️ Semantic NTP Peer Management

**Add custom NTP server:**

* Strict semantic validation for IPv4, IPv6, and FQDN addresses (rejects loopback, multicast, broadcast, and invalid DNS labels).
* Pre-flight DNS resolution and live UDP 123 latency and Stratum probe before saving to registry.
* Domain controller safety: does not set `/reliable:yes` on client workstations to prevent domain-wide time pollution.

**Remove peers:**

* Remove individual configured peers with registry validation.
* Safety checks prevent accidentally removing the last configured time source.

**One-click presets:**

* **⭐ Iran-Optimized:** Balanced combination of public international and local `.ir` NTP servers.
* **🌐 Global Tier-1:** Cloudflare, Google, and NTP Pool.
* **🪟 Windows Default:** Restores standard `time.windows.com`.

### 4. 📅 Custom System Time

* Set a custom year, month, day, hour, and minute with sub-second accuracy.
* Confirmation prompt for changes greater than 24 hours to prevent breaking active SSL/TLS web sessions.
* Quick one-click reset to current system time.

### 5. ⚡ GUI + CLI

* **GUI (`WinTime.exe`):** Lightweight Windows Forms desktop interface with live status feedback.
* **CLI (`WinTime.ps1`):** Standalone PowerShell engine featuring identical RFC 4330 SNTP 4-timestamp math, multi-source consensus, peer management, and Windows Time service repair.

---

## 📁 Repository Structure

```text
├── WinTime.exe                 # Main executable (~155 KB)
├── WinTime.ps1                 # PowerShell CLI and management engine
├── Program.cs                  # Windows Forms UI and Application logic
├── Core/                       # Production core modules
│   ├── NtpPacket.cs            # RFC 4330 / 5905 SNTP packet serialization & 4-timestamp math
│   ├── NtpClient.cs            # Multi-source consensus & outlier filtering
│   ├── ProcessRunner.cs        # Safe subprocess runner with strict exit code checking
│   ├── PeerValidator.cs        # Semantic IPv4/IPv6/FQDN validation & sanitization
│   ├── NativeMethods.cs        # Win32 token privilege management & sub-second clock API
│   ├── HttpsTimeClient.cs      # Fallback HTTP Date header parser with TLS skew diagnosis
│   └── TimeServiceManager.cs   # Windows Time service configuration & domain detection
├── Tests/                      # Automated unit test suite
│   ├── UnitTests.cs            # Protocol correctness & security regression tests
│   └── run_tests.bat           # Command-line test runner
├── app.manifest                # UAC elevation and Per-Monitor DPI V2 configuration
├── build.bat                   # Native compilation script
├── install.ps1                 # Verified one-click installer
├── .github/workflows/build.yml # CI workflow with automated test suite
├── README.md                   # English documentation
├── README.fa.md                # Persian documentation
└── LICENSE                     # MIT License
```

---

## 🔨 Building Locally

WinTime does not require Visual Studio.

To compile:

```cmd
build.bat
```

The script compiles `Program.cs` and `Core\*.cs` using the built-in Windows C# compiler into `WinTime.exe`.

To run the automated unit test suite:

```cmd
Tests\run_tests.bat
```

---

## 🤖 Automated GitHub Actions Build

Continuous integration workflow executes on every commit:

1. Runs the 16 automated unit tests for SNTP packet math, peer validation, and process execution.
2. Validates syntax for all PowerShell scripts (`WinTime.ps1`, `install.ps1`).
3. Compiles the native executable and publishes build artifacts and release binaries.

---

## 🇮🇷 Regional NTP Connectivity

Certain networks or ISPs may restrict or throttle UDP port 123 (NTP).

WinTime addresses this through:
- Local Iranian NTP peers in the Iran-Optimized preset (`time.digiboy.ir`, `ntp.iranet.ir`).
- Automatic failover to HTTPS Date header synchronization (port 443) when UDP 123 is blocked.

---

## 📜 License

WinTime is released under the [MIT License](LICENSE).

You are free to use, modify, distribute, and use the software commercially, subject to the terms of the license.
