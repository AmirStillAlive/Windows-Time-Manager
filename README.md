# ⚡ WinTime — Windows Time & NTP Manager

<p align="center">
  <img src="assets/logo.png" width="160" height="160" alt="WinTime Logo" />
</p>

<p align="center">
  <a href="README.md"><b>English</b></a> | <a href="README.fa.md"><b>فارسی</b></a>
</p>

> A lightweight Windows utility for system clock synchronization, NTP peer management, and custom time adjustments.

[![Build](https://github.com/AmirStillAlive/Windows-Time-Manager/actions/workflows/build.yml/badge.svg?branch=dev)](https://github.com/AmirStillAlive/Windows-Time-Manager/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue)](https://www.microsoft.com/windows)
[![Version](https://img.shields.io/badge/version-v0.1.2--alpha-orange)](#)

---

> [!WARNING]
> **Alpha / Work in Progress Notice (v0.1.2)**
> This tool is in early development. While the core features are tested, it is an experimental utility and may have bugs, unhandled edge cases, or rough edges on certain Windows configurations. Please use it with that in mind, and feel free to report issues or suggest improvements.

---

## 📖 The Backstory: How It Started

This project was born out of a simple, everyday annoyance with **Red Dead Redemption 2 (RDR2)**. 

Due to a well-known launch / activation bug with the game, players often need to roll back their Windows clock to an earlier date (specifically `2019-10-15 21:31:00`) just to launch the game, and then sync the clock back to the correct real-world time afterwards. Doing this repeatedly through Windows Settings or Control Panel became tedious.

What started as a tiny quick-fix script for that game issue gradually grew: Windows users often encounter broken `w32time` services, desynced system clocks, or blocked UDP port 123 (NTP) on certain network providers. WinTime evolved into a general-purpose, lightweight tool to handle these situations cleanly.

---

## 🌟 Features

### 1. 🔄 Multi-Source System Clock Synchronization
* **Windows Time Service (`w32tm`):** Tries native resynchronization using configured system peers.
* **Direct SNTP Consensus (RFC 4330 / 5905):** If Windows Time fails, WinTime queries multiple public NTP servers (`time.cloudflare.com`, `time.google.com`, `pool.ntp.org`, `time.windows.com`), computes round-trip delay and offset using standard 4-timestamp math, filters outliers, and applies the median offset.
* **HTTPS Date Header Fallback:** When UDP port 123 is completely blocked by restrictive networks, WinTime falls back to secure HTTP Date headers (port 443) with clock-skew detection.

### 2. ⚙️ NTP Peer Management
* **Manage Peers:** View, add, and remove configured time servers in the Windows registry.
* **Input Validation:** Checks IPv4, IPv6, and FQDN addresses before applying to avoid misconfiguration.
* **Ready Presets:**
  * **⭐ Iran-Optimized:** Includes reliable local and international servers.
  * **🌐 Global Tier-1:** Cloudflare, Google, and NTP Pool.
  * **🪟 Windows Default:** Restores the default `time.windows.com`.

### 3. 📅 Custom Time Adjustments
* Manually adjust the date and time when testing or troubleshooting.
* Includes safety confirmations if shifting the clock by more than 24 hours (to prevent accidental SSL/TLS certificate failures).
* Instant one-click restore to real time.

### 4. 🎮 Game Workarounds (RDR2 Preset)
* Includes a quick preset button to set the date to `2019-10-15 21:31:00` for the RDR2 launch workaround, alongside a one-click button to restore the accurate network time right after.

### 5. 💻 GUI & CLI Options
* **GUI (`WinTime.exe`):** Standalone desktop application with real-time status display and dark theme.
* **CLI (`WinTime.ps1`):** Standalone PowerShell script for command-line users or automation.

---

## 📁 Repository Structure

```text
├── WinTime.ps1                 # Standalone PowerShell script
├── Program.cs                  # Windows Forms UI application (C#)
├── Core/                       # Core modules
│   ├── NtpPacket.cs            # RFC 4330 / 5905 SNTP packet handling & math
│   ├── NtpClient.cs            # Multi-source NTP consensus & outlier filtering
│   ├── ProcessRunner.cs        # Process runner with exit-code validation
│   ├── PeerValidator.cs        # IPv4/IPv6/FQDN validation
│   ├── NativeMethods.cs        # Win32 privilege & system time APIs
│   ├── HttpsTimeClient.cs      # Fallback HTTPS Date header client
│   └── TimeServiceManager.cs   # Windows Time service configuration
├── Tests/                      # Unit test suite
│   ├── UnitTests.cs            # Protocol correctness & validation tests
│   └── run_tests.bat           # Test runner
├── app.manifest                # UAC elevation and DPI awareness manifest
├── app.ico                     # Application icon
├── build.bat                   # Local build script (uses csc.exe)
├── install.ps1                 # One-liner web installer
├── .github/workflows/build.yml # CI workflow for tests & release packaging
├── README.md                   # English documentation
├── README.fa.md                # Persian documentation
└── LICENSE                     # MIT License
```

*(Note: Pre-compiled binaries like `WinTime.exe` are published under [Releases](https://github.com/AmirStillAlive/Windows-Time-Manager/releases) rather than tracked in git.)*

---

## 🔨 Building Locally

WinTime does not require Visual Studio or any third-party SDK. It compiles using the standard Microsoft C# compiler included with Windows.

To build:
```cmd
build.bat
```

To run the test suite:
```cmd
Tests\run_tests.bat
```

---

## 📜 License

WinTime is licensed under the [MIT License](LICENSE).
