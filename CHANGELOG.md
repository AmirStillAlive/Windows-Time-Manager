# Changelog

All notable changes to the WinTime project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.3] - 2026-09-20

### Security
- **Strict TLS 1.2 / TLS 1.3 Enforcement:** Explicitly restricted HTTPS date synchronization to TLS 1.2 and TLS 1.3 in both C# (`HttpsTimeClient.cs`) and PowerShell (`WinTime.ps1`, `install.ps1`), disabling insecure SSLv3, TLS 1.0, and TLS 1.1 protocols.
- **On-Demand Privilege Elevation (`asInvoker`):** Changed `app.manifest` execution level from `requireAdministrator` to `asInvoker`. WinTime GUI now opens seamlessly for standard users and requests UAC elevation on-demand only when applying administrative settings.
- **Hardened Privilege Lifetime:** Reworked `NativeMethods.cs` privilege adjustment. `SeSystemtimePrivilege` is enabled immediately prior to `SetSystemTime`, reverted in a `finally` block with zeroed attributes (`SE_PRIVILEGE_REMOVED`), and token handles are properly closed.
- **Active Directory Domain Protection:** Added enterprise domain membership detection via `NetGetJoinInformation`. Domain-joined machines now block accidental NTP peer reconfiguration unless explicitly acknowledged via the new UI override option (`allowDomainOverride`).
- **Download Integrity & Authenticode Verification:** Updated `install.ps1` to download `SHA256SUMS.txt`, verify SHA-256 hashes of `WinTime.exe` and `WinTime.ps1` before execution, and validate Authenticode signatures with warnings for unsigned code and aborts on invalid signatures.
- **CI Checksum Publishing:** Updated `.github/workflows/build.yml` to automatically compute and publish `SHA256SUMS.txt` alongside release assets.

### Fixed
- **Modern HTTP Client & Multi-Format Date Parsing:** Replaced legacy `HttpWebRequest` in `HttpsTimeClient.cs` with `HttpClient`. Implemented support for RFC 1123, RFC 850, and ANSI C `asctime()` HTTP Date header specifications.
- **NTP Year 2036 Rollover:** Addressed the 32-bit NTP seconds rollover occurring on 2036-02-07 06:28:16 UTC in `NtpPacket.cs` using an RFC 4330 era-inference heuristic (`intPart < 0x80000000UL` mapping to Era 1).
- **ProcessRunner Timeout & Kill:** Ensured `ProcessRunner.cs` cleanly terminates child processes on timeout and waits for exit, preventing zombie or hanging processes from stalling operations.
- **Query Caching:** Added a 5-second in-memory cache to `TimeServiceManager.GetSystemPeers` for `w32tm /query /source` calls, eliminating repetitive process spawning on UI redraws.
- **UI Tooltip Diagnostics:** Fixed silent button disabling in `Program.cs`. Action buttons now display informative tooltips explaining validation failures, missing prerequisites, or error details.
- **CLI Startup & Self-Elevation Fixes:** Wrapped `WinTime.ps1` in a root exception handler with interactive pause, preventing silent window closures on startup errors. Fixed self-elevation handling when paths contain spaces or unresolvable scripts.

### Changed
- **CLI Launcher Batch File:** Added root `WinTime.bat` launcher to allow simple double-click execution of `WinTime.ps1` with `-NoProfile -ExecutionPolicy Bypass`.
- **Dynamic Tag Resolution:** Configured `install.ps1` to query the GitHub Releases API for the latest release tag with a hardcoded fallback to `v0.1.3`.
- **UI Concurrency Protection:** Integrated `SemaphoreSlim` in `Program.cs` to prevent overlapping concurrent operations from corrupting system time service state.
- **Extended Test Suite:** Expanded unit tests from 16 to 20 tests in `Tests/UnitTests.cs`, covering NTP 2036 rollover, RFC 1123/850/ANSI C date parsing, process runner timeout killing, and domain policy override seams.
- **Version Bump:** Bumped version across all components (`Program.cs`, `app.manifest`, `WinTime.ps1`, `install.ps1`, `README.md`, `README.fa.md`) to `0.1.3`.

## [0.1.1] - 2026-09-19

- Initial alpha release of WinTime GUI and CLI.
