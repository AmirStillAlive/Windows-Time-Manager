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
[![Version](https://img.shields.io/badge/version-v0.1.3--alpha-orange)](#)

---

> [!WARNING]
> **Alpha / Work in Progress Notice (v0.1.3)**
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
* **Direct SNTP Consensus (RFC 4330 / 5905):** If Windows Time fails, WinTime queries multiple public NTP servers (`time.cloudflare.com`, `time.google.com`, `pool.ntp.org`, `time.windows.com`, `time.aws.com`), computes round-trip delay and offset using standard 4-timestamp math, filters outliers, and applies the median offset.
* **HTTPS Date Header Fallback:** When UDP port 123 is completely blocked by restrictive networks, WinTime falls back to secure HTTP Date headers (port 443) with clock-skew detection.

### 2. ⚙️ NTP Peer Management
* **Manage Peers:** View, add, and remove configured time servers in the Windows registry.
* **Input Validation:** Checks IPv4, IPv6, and FQDN addresses before applying to avoid misconfiguration.
* **Domain Protection:** Detects Active Directory domain membership to avoid disrupting enterprise Kerberos time hierarchies without explicit administrative approval.
* **Ready Presets:**
  * **⭐ Iran-Optimized:** Includes reliable local and international servers.
  * **🌐 Global Tier-1:** Cloudflare, Google, NTP Pool, and AWS.
  * **🪟 Windows Default:** Restores the default `time.windows.com`.

### 3. 📅 Custom Time Adjustments
* Manually adjust the date and time when testing or troubleshooting.
* Includes safety confirmations if shifting the clock by more than 24 hours (to prevent accidental SSL/TLS certificate failures).
* Instant one-click restore to real time.

### 4. 🎮 Game Workarounds (RDR2 Preset)
* Includes a quick preset button to set the date to `2019-10-15 21:31:00` for the RDR2 launch workaround, alongside a one-click button to restore the accurate network time right after.

### 5. 💻 GUI & CLI Options
* **GUI (`WinTime.exe`):** Standalone desktop application with real-time status display and dark theme. Runs without requiring initial UAC elevation (`asInvoker`), elevating on-demand when administrative actions are invoked.
* **CLI Launcher (`WinTime.bat`):** Batch launcher that opens the CLI with `-NoProfile -ExecutionPolicy Bypass`. Simply double-click to run.
* **CLI (`WinTime.ps1`):** Standalone PowerShell script for command-line users or automation. Can also be launched by right-clicking `WinTime.ps1` and choosing **"Run with PowerShell"**.

---

## 🚀 Installation

Choose any of the installation or launch methods below:

### Method 1: Standalone Graphical App (Recommended)
Download the standalone executable **[`WinTime.exe`](https://github.com/AmirStillAlive/Windows-Time-Manager/releases/latest)** directly from the latest [GitHub Release](https://github.com/AmirStillAlive/Windows-Time-Manager/releases).
* **No installation or PowerShell commands required.** Simply double-click `WinTime.exe` to launch the modern graphical interface.
* Runs without initial UAC elevation (`asInvoker`), requesting elevation only when administrative actions are performed.

### Method 2: Automated Batch Installer (`install.bat`)
Download and run **[`install.bat`](https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/install.bat)**, or run the following command in Command Prompt (CMD) or PowerShell:
```cmd
curl -sSfL https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/install.bat -o "%temp%\install.bat" && "%temp%\install.bat"
```
> **Note:** The batch installer uses Windows' native `curl.exe` to download components into `%LOCALAPPDATA%\WinTime`, clears Mark-of-the-Web flags, creates Desktop and Start Menu shortcuts, and provides an uninstaller. Because it does not stream code directly into memory (`iex`), it avoids heuristic antivirus false positives.

### Method 3: Offline / Manual Script Download (CLI)
1. Download both [`WinTime.bat`](https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/WinTime.bat) and [`WinTime.ps1`](https://raw.githubusercontent.com/AmirStillAlive/Windows-Time-Manager/main/WinTime.ps1) (or download packaged assets from the latest [GitHub Release](https://github.com/AmirStillAlive/Windows-Time-Manager/releases)) and place them in the same folder.
2. **Double-click `WinTime.bat`**. The batch launcher automatically bypasses restrictive execution policies, clears Mark-of-the-Web metadata, and starts the CLI.
> **Note:** Web browsers mark downloaded files with Mark-of-the-Web (MOTW). Use `WinTime.bat` to launch or unblock the script before direct PowerShell execution.

### Method 4: Advanced Command-Line Launch
If running PowerShell directly from the terminal:
```cmd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\path\to\WinTime.ps1"
```

---

## 🔍 Verifying Downloads

WinTime publishes cryptographic SHA-256 checksums in `SHA256SUMS.txt` attached to every [GitHub Release](https://github.com/AmirStillAlive/Windows-Time-Manager/releases). Published checksums cover release assets (packaged `.exe`, `.ps1`, `.bat`), not dynamic raw git branch blobs.

You can independently verify file integrity using PowerShell:

```powershell
Get-FileHash .\WinTime.exe, .\WinTime.ps1, .\WinTime.bat -Algorithm SHA256 | Format-Table -AutoSize
```
Compare the output hashes with the published entries in `SHA256SUMS.txt` from the corresponding GitHub Release.

---

## 🛠️ Troubleshooting

### 1. Script is blocked: "File ... cannot be loaded because running scripts is disabled" or "is not digitally signed"
* **Symptom:**
  ```text
  .\WinTime.ps1 : File C:\...\WinTime.ps1 cannot be loaded.
  The file C:\...\WinTime.ps1 is not digitally signed. You cannot run this script on the
  current system. ... FullyQualifiedErrorId : UnauthorizedAccess
  ```
* **Why this happens:** Files downloaded from web browsers carry Windows **Mark-of-the-Web (MOTW)** (`Zone.Identifier: ZoneId=3`). When your PowerShell execution policy is `RemoteSigned`, Windows blocks unsigned scripts downloaded from the internet *at load time*, before any script code can run.
* **Remedies:**
  * **Option A (Simplest):** Launch using `WinTime.bat` instead of the `.ps1` file.
  * **Option B:** Unblock the downloaded script file:
    ```powershell
    Unblock-File -LiteralPath .\WinTime.ps1
    ```
  * **Option C:** Check current execution policies:
    ```powershell
    Get-ExecutionPolicy -List
    ```
    To permit local scripts for your current user session:
    ```powershell
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
    ```

### 2. Parenthesized duplicate filenames: `.\WinTime (2).ps1`
* **Symptom:** You download multiple copies of the script and run:
  ```powershell
  .\WinTime (2).ps1
  ```
* **Why this happens:** PowerShell argument parsing splits `.\WinTime (2).ps1` into the command `.\WinTime` and argument `(2).ps1`. PowerShell automatically appends `.ps1` to the command name and executes `.\WinTime.ps1` instead of `(2)`.
* **Remedy:** Always invoke files with spaces or parentheses using the call operator `&` and quotes:
  ```powershell
  & ".\WinTime (2).ps1"
  ```
  *(Or delete duplicate numbered files and use `WinTime.bat`.)*

### 3. Extracting from ZIP Archives
If you downloaded WinTime inside a `.zip` archive, Windows applies MOTW to every extracted file. To avoid issues, unblock the `.zip` archive **before** extracting:
1. Right-click the `.zip` file -> **Properties**.
2. Check **Unblock** at the bottom of the General tab, then click **OK**.
3. Alternatively, run:
   ```powershell
   Unblock-File -LiteralPath .\Windows-Time-Manager.zip
   ```

---

## 📁 Repository Structure

```text
├── assets/                     # Project visual assets & branding
│   └── logo.png                # WinTime logo
├── WinTime.bat                 # Easy double-click batch launcher for CLI
├── WinTime.ps1                 # Standalone PowerShell CLI engine
├── Program.cs                  # Windows Forms UI application (C#)
├── Core/                       # Core engine modules
│   ├── NtpPacket.cs            # RFC 4330 / 5905 SNTP packet handling & math
│   ├── NtpClient.cs            # Multi-source NTP consensus & outlier filtering
│   ├── ProcessRunner.cs        # Process runner with exit-code validation & process-tree cleanup
│   ├── PeerValidator.cs        # IPv4/IPv6/FQDN validation
│   ├── NativeMethods.cs        # Win32 privilege & system time APIs
│   ├── HttpsTimeClient.cs      # Fallback HTTPS Date header client
│   └── TimeServiceManager.cs   # Windows Time service configuration
├── Tests/                      # Automated unit test suite
│   ├── UnitTests.cs            # Protocol correctness & validation tests
│   ├── test_parity.ps1         # PowerShell CLI behavioural parity test suite
│   └── run_tests.bat           # Test runner
├── app.manifest                # Invoker execution level & DPI awareness manifest
├── app.ico                     # Application icon
├── build.bat                   # Local build script (uses in-box csc.exe)
├── install.ps1                 # One-liner web installer with hash & signature checks
├── .gitattributes              # Deterministic line-ending specifications
├── SECURITY.md                 # Security policy & threat disclosures
├── CHANGELOG.md                # Version release notes & changelog
├── .github/workflows/build.yml # CI workflow for tests & release packaging
├── README.md                   # English documentation
├── README.fa.md                # Persian documentation
└── LICENSE                     # MIT License
```

*(Note: Pre-compiled binaries like `WinTime.exe` and `SHA256SUMS.txt` are published under [Releases](https://github.com/AmirStillAlive/Windows-Time-Manager/releases) rather than tracked directly in git.)*

---

## ⚠️ Known Limitations

1. **HTTPS Date Header Granularity:** The fallback HTTPS synchronization path relies on standard HTTP `Date` response headers, which provide ~1-second coarse granularity rather than sub-millisecond NTP precision.
2. **Active Directory Domain Membership:** On domain-joined machines, Windows Time synchronization is managed by Active Directory Domain Controllers via Kerberos policies. Manual NTP peer overrides are blocked by default to prevent breaking domain authentication.
3. **Execution Policy & Group Policy (GPO):** While `-ExecutionPolicy Bypass` resolves default and per-user execution restrictions, enterprise Group Policies (`MachinePolicy` / `UserPolicy` enforced by domain administrators) override process-level bypass flags.

---

## 🔨 Building Locally

WinTime does not require Visual Studio or any third-party SDK. It compiles using the standard Microsoft C# compiler included with Windows.

To build:
```cmd
build.bat
```

To run the automated unit test suite:
```cmd
Tests\run_tests.bat
```

To run the PowerShell parity test suite:
```powershell
powershell -ExecutionPolicy Bypass -File Tests\test_parity.ps1
```

---

## 📜 License

WinTime is licensed under the [MIT License](LICENSE).
