# Changelog

All notable changes to the WinTime project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.3] - 2026-09-20

### Security
- **Strict TLS 1.2 / TLS 1.3 Enforcement:** Explicitly restricted HTTPS date synchronization to TLS 1.2 and TLS 1.3 in both C# (`HttpsTimeClient.cs`) and PowerShell (`WinTime.ps1`, `install.ps1`), disabling insecure SSLv3, TLS 1.0, and TLS 1.1 protocols.
- **On-Demand Privilege Elevation (`asInvoker`):** Changed `app.manifest` execution level from `requireAdministrator` to `asInvoker`. WinTime GUI now opens seamlessly for standard users and requests UAC elevation on-demand only when applying administrative settings.
- **Hardened Privilege Lifetime:** Reworked `NativeMethods.cs` privilege adjustment. `SeSystemtimePrivilege` is enabled immediately prior to `SetSystemTime`, reverted in a `finally` block with zeroed attributes (`SE_PRIVILEGE_REMOVED`), and token handles are properly closed.
- **Active Directory Domain Protection:** Added enterprise domain membership detection via `NetGetJoinInformation` in C# and `Win32_ComputerSystem` in PowerShell. Domain-joined machines now block accidental NTP peer reconfiguration unless explicitly bypassed via `chkAllowDomainOverride` or `-AllowDomainOverride`.
- **Download Integrity & Authenticode Verification:** Reworked `install.ps1` to download `SHA256SUMS.txt` from the same base as payloads, verify SHA-256 hashes of all components before execution, and validate Authenticode signatures with warnings for unsigned code and aborts on invalid signatures.
- **Line Ending Normalization (`.gitattributes`):** Pinned deterministic LF line endings for scripts, C# code, manifests, and documentation, and CRLF for batch files, resolving CRLF vs LF hash mismatches across git blobs, CI checkouts, and raw downloads.
- **CI Integrity Verification Job:** Added a dedicated `verify-integrity` job to `.github/workflows/build.yml` that re-checks computed SHA-256 hashes against all shipped artifacts before release. Added automated version-consistency checks.
- **Security Policy:** Added `SECURITY.md` documenting privilege models, hash verification, MOTW execution policies, and reporting guidelines.

### Fixed
- **Mark-of-the-Web (MOTW) & RemoteSigned Execution Failure:** Fixed execution failure where unsigned `.ps1` files downloaded from the internet were blocked by PowerShell under `RemoteSigned`. Hardened `WinTime.bat` to clear MOTW before launching and execute with `-ExecutionPolicy Bypass`.
- **Modern HTTP Client & Multi-Format Date Parsing:** Replaced legacy `HttpWebRequest` in `HttpsTimeClient.cs` and modernized `WinTime.ps1` `Get-HttpsTimeFromHost` with `HttpClient`. Added robust parsing for RFC 1123, RFC 850, and ANSI C `asctime()` HTTP Date header formats.
- **NTP Year 2036 Rollover:** Addressed the 32-bit NTP seconds rollover occurring on 2036-02-07 06:28:16 UTC in both `Core/NtpPacket.cs` and `WinTime.ps1` using an RFC 4330 era-inference heuristic (`intPart < 0x80000000UL` mapping to Era 1).
- **ProcessRunner Timeout & Kill:** Ensured `ProcessRunner.cs` cleanly terminates child processes on timeout and waits for exit, preventing zombie or hanging processes from stalling operations.
- **Query Caching:** Added a 5-second in-memory cache to `TimeServiceManager.GetSystemPeers` for `w32tm /query /source` calls, eliminating repetitive process spawning on UI redraws.
- **UI Tooltip Diagnostics:** Fixed silent button disabling in `Program.cs`. Action buttons now display informative tooltips explaining validation failures, missing prerequisites, or error details.
- **CLI Startup & Self-Elevation Fixes:** Set `[Console]::OutputEncoding` as the first statement, enabled `$ErrorActionPreference = 'Stop'`, preserved current directory on elevation, forwarded arguments, and cleanly handled user-cancelled UAC elevation (Win32 error 1223).
- **One-Liner Installer Fallback Chain:** Fixed `install.ps1` 404 failure when GitHub Releases API is rate-limited or unavailable by adding a probe-validated candidate source chain (release assets -> raw@tag -> raw@dev -> raw@main).

### Added
- **Hardened CLI Launcher (`WinTime.bat`):** Root batch wrapper with `setlocal EnableExtensions`, `cd /d "%~dp0"`, MOTW unblocking, `pwsh.exe` fallback, argument forwarding, and error pause.
- **Start Menu Shortcuts & Uninstaller:** `install.ps1` now creates Start Menu shortcuts and installs an `uninstall.ps1` script to cleanly remove application files and shortcuts.
- **Comprehensive Documentation:** Added Installation, Troubleshooting (MOTW, RemoteSigned, duplicate filenames, ZIP extraction), and Verification sections to both `README.md` and `README.fa.md`.

### Changed
- **Extended Test Suite:** Expanded unit tests in `Tests/UnitTests.cs` to 20 automated tests covering NTP 2036 rollover, RFC 1123/850/ANSI C date parsing, process runner timeout killing, and domain policy override seams.
- **Version Alignment:** Synchronized version `0.1.3` across all GUI, CLI, installer, manifest, CI, and documentation components.

## [0.1.2] - 2026-09-19

### Changed
- **UI Menu Reorganization:** Demoted the RDR2 game fix button and menu entry to the bottom preset in both GUI (`Program.cs`) and CLI (`WinTime.ps1`) to emphasize general-purpose NTP synchronization.
- **CI Pre-Release Workflow:** Configured GitHub Actions release automation to publish alpha builds as pre-releases.

## [0.1.1] - 2026-09-19

- Initial alpha release of WinTime GUI and CLI.
