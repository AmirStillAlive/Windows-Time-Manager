
# ⚡ WinTime — Windows Time & NTP Manager

<p align="center">
  <img src="assets/logo.png" width="160" height="160" alt="WinTime Logo" />
</p>

<p align="center">
  <a href="README.md"><b>English</b></a> | <a href="README.fa.md"><b>فارسی</b></a>
</p>

> A lightweight and fast Windows utility for system clock synchronization, NTP peer management, custom time presets, and game-specific time presets such as **Red Dead Redemption 2**.

[![Build](https://github.com/AmirStillAlive/Windows-Time-Manager/actions/workflows/build.yml/badge.svg)](https://github.com/AmirStillAlive/Windows-Time-Manager/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/AmirStillAlive/Windows-Time-Manager)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue)](https://www.microsoft.com/windows)
[![Language](https://img.shields.io/badge/language-C%23-purple)](#)


> ⚠️ **WinTime is currently in alpha. Expect bugs, breaking changes, and incomplete features. No guarantees are provided at this stage.**
---

## Why WinTime?

* **🚀 Tiny executable (~200 KB):** A compact Windows utility with no bundled third-party runtime, interpreter, or large framework package.

* **⚡ Fast startup:** Designed for quick launch and minimal overhead.

* **✨ Modern Windows UI:** A clean dark interface with custom vector icons and DPI-aware layout for different Windows display scaling settings.

* **🎯 Live feedback:** Every operation provides immediate status feedback such as `Loading`, `Success`, or `Error`.

* **🔍 Open Source & Transparent:** The complete source code is available in this repository for inspection and building.

---

## Features

### 1. 🎮 One-Click Game Time Presets

* Sets the system clock to the predefined `2019-10-15 21:31:00` timestamp used by the RDR2 preset.

* Uses the Windows `SeSystemtimePrivilege` required for changing the system clock.

### 2. 🔄 System Time Synchronization

* **Windows Time (`w32tm`):** Resynchronizes the system using the currently configured Windows Time peers with `w32tm /resync /force`.

* **Public NTP services:** Supports time synchronization and connectivity testing against services such as `time.cloudflare.com`, `time.google.com`, `pool.ntp.org`, and `time.windows.com`.

* **HTTPS fallback:** When UDP port 123 is unavailable, the application can use an HTTPS-based time source, depending on the configured provider.

### 3. ⚙️ NTP Peer Management

**Add custom NTP server:**

* DNS resolution check before adding a peer.
* UDP port 123 connectivity test.
* Basic hostname validation.
* Configures the Windows Time service using the appropriate NTP client configuration.

**Remove peers:**

* Remove individual configured peers.
* Safety checks help prevent accidentally removing the last usable time source.

**One-click presets:**

* **⭐ Iran-Optimized:** A combination of public international and regional NTP servers.
* **🌐 Global:** Cloudflare, Google, and NTP Pool.
* **🪟 Windows Default:** Restores `time.windows.com`.

### 4. 📅 Custom System Time

* Set a custom year, month, day, hour, and minute.
* Quickly return to the current system time.

### 5. ⚡ GUI + CLI

* **GUI (`WinTime.exe`):** A lightweight Windows desktop interface.
* **CLI (`WinTime.ps1`):** A PowerShell-based command-line engine with peer management and Windows Time service repair functionality, including peer backup and restore.

---

## 📁 Repository Structure

```text
├── WinTime.exe                 # Main executable (~60 KB)
├── WinTime.ps1                 # PowerShell CLI and management engine
├── Program.cs                  # Complete C# source code
├── app.manifest                # UAC and DPI configuration
├── build.bat                   # Local build script
├── .github/workflows/build.yml # GitHub Actions build workflow
├── README.md                   # English documentation
├── README.fa.md                # Persian documentation
└── LICENSE                     # MIT License
```

---

## 🔨 Building Locally

WinTime does not require Visual Studio.

Run:

```cmd
build.bat
```

The script compiles `Program.cs` into `WinTime.exe`.

> Build requirements depend on the compiler and .NET Framework components available on the Windows installation. The repository does not bundle a third-party compiler or runtime.

---

## 🤖 Automated GitHub Actions Build

When changes are pushed to GitHub:

1. A clean Windows runner is started.
2. The application is compiled from the repository source.
3. The resulting executable can be attached to GitHub Releases.

---

## 🇮🇷 Regional NTP Connectivity

Some networks may restrict or interfere with UDP port 123, which is the standard port used by NTP.

WinTime therefore provides alternative NTP peers and an HTTPS-based fallback where supported by the configured time source.

The **Iran-Optimized** preset is intended to provide additional regional connectivity options. Actual availability and latency depend on the user's ISP and network conditions.

---

## 📜 License

WinTime is released under the [MIT License](LICENSE).

You are free to use, modify, distribute, and use the software commercially, subject to the terms of the license.
