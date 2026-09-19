# Changelog

All notable changes to the WinTime project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.3] - 2026-09-20

### Security
- **Fail-Closed Download Integrity:** Updated `install.ps1` to download `SHA256SUMS.txt` from the same source base as payloads, enforce fail-closed verification (aborting immediately if checksums are missing, empty, or mismatched), and validate Authenticode signatures. Removed static `SHA256SUMS.txt` from repository tracking in favor of CI-generated release artifacts.
- **On-Demand Elevation (`asInvoker`):** Switched `app.manifest` execution level from `requireAdministrator` to `asInvoker`. Standard users can open WinTime to inspect logs, view configured peers, and test latencies without triggering initial UAC prompts, with elevation requested on-demand only for clock modification or registry changes.
- **Active Directory Protection:** Added domain membership detection (`NetGetJoinInformation` in C# and `Win32_ComputerSystem` in PowerShell). Modifying NTP peers on domain-joined systems is blocked by default to prevent disrupting Kerberos time hierarchies, with explicit UI/CLI override controls (`chkAllowDomainOverride` / `-AllowDomainOverride`).
- **Hardened Privilege Lifetime:** Updated `NativeMethods.cs` to acquire `SeSystemtimePrivilege` exclusively around the `SetSystemTime` Win32 API call, reverting privilege attributes in a `finally` block and safely closing token handles.
- **Strict TLS Protocols:** Enforced TLS 1.2 and TLS 1.3 across all HTTPS time synchronization clients (`HttpsTimeClient.cs`, `WinTime.ps1`, `install.ps1`).
- **Deterministic Line Endings (`.gitattributes`):** Configured repository `.gitattributes` pinning LF line endings for C# source, PowerShell scripts, manifests, and documentation, and CRLF for batch scripts.
- **CI Release Integrity Pipeline:** Split GitHub Actions workflow (`build.yml`) into sequential `build` -> `verify-integrity` -> `publish` jobs with top-level concurrency control, promoted signing secret environment variables, `/warn:4 /warnaserror` compiler flags, and raw-serving byte verification.
- **Security Policy:** Added `SECURITY.md` documenting privilege architecture, hash verification procedures, MOTW execution policies, and vulnerability disclosure guidelines.

### Fixed
- **ThreadPool Exception Handling & Lock Safety (P0-1):** Added `UiInvoke` helper with `IsDisposed` and `IsHandleCreated` guards in `Program.cs`. Wrapped all background worker delegates in comprehensive exception handling and moved `_operationLock.Release()` out of UI dispatch into `finally` blocks, preventing application hangs and crashes.
- **NtpClient Race Condition & DNS Timeout (P0-2):** Replaced `CountdownEvent` with `ManualResetEventSlim` and `Interlocked.Decrement` in `NtpClient.QueryMultipleSources`. Added timeout-bounded asynchronous DNS resolution via `Dns.BeginGetHostAddresses` and thread-safe results snapshotting, preventing `ObjectDisposedException` and hanging queries.
- **ProcessRunner Process Tree Cleanup (P2-7):** Updated `ProcessRunner.cs` to terminate entire process trees using `taskkill.exe /T /F /PID` prior to process kill, and ensured elapsed execution time is accurately recorded on timeout paths.
- **Parse-Time ERRORLEVEL Expansion (P2-1, P2-2):** Resolved parse-time expansion bugs in `WinTime.bat` and `build.bat` by enabling `EnableDelayedExpansion` and using `!ERRORLEVEL!` and `if not errorlevel 1` inside nested blocks.
- **DPI Layout & Window Scaling (P1-2, P1-3):** Configured `<dpiAware>true</dpiAware>` (System DPI aware) in `app.manifest`, set `AutoScaleMode.Dpi` on `MainForm`, scaled client and minimum dimensions by DPI factor (with base minimum reduced to 760x560 for 1280x720 effective desktops), anchored tab controls, and made `CustomDarkConsole.LineHeight`, column offsets, and scrollbars font-derived.
- **UI Responsiveness During Peer Operations (P1-1):** Offloaded DNS resolution, NTP reachability queries, and `w32tm` service updates in custom peer addition and presets from the UI thread to background workers.
- **NTP 64-bit Timestamp Accuracy & 2036 Rollover (P3-1, P3-2):** Converted `NtpPacket.ReadTimestamp` to 64-bit tick math (`TimeSpan.TicksPerSecond`) avoiding 1ms double quantization. Added `MinPlausibleServerTime` constant (2020-01-01 UTC) and corrected error diagnostics. Handled 2036 era rollover in C# and PowerShell via era-inference heuristic.
- **HTTP Date Header Parsing Parity (P1-6):** Extracted `ConvertFrom-HttpDateHeader` in `WinTime.ps1` for parsing RFC 1123, RFC 850, and ANSI C `asctime()` formats, and updated `Tests/test_parity.ps1` to test the production parser directly with negative testing for invalid input.
- **Single Instance Enforcement (P2-5):** Added named Mutex (`Global\WinTime.SingleInstance` with fallback to `Local\WinTime.SingleInstance`) preventing duplicate concurrent clock-modifying instances.
- **Unhandled Application Exceptions (P2-6):** Attached global exception handlers to `Application.ThreadException` and `AppDomain.CurrentDomain.UnhandledException` logging to `%LOCALAPPDATA%\WinTime\crash.log`.
- **UI Polish & Diagnostics (P2-4, P3-3, P3-4, P3-5, P3-6, P3-7):** Guarded `Clipboard.SetText` against empty text and clipboard locks; preserved explicit caller log colors in `CustomDarkConsole`; capped console log buffer at 2000 lines with FIFO trimming; disposed `clockTimer` on form closed; showed domain override checkbox only on domain-joined systems; and reported "✗ All Peers Failed" when all tested peer latencies fail.
- **PE Assembly Metadata (P2-8):** Added `AssemblyTitle`, `AssemblyProduct`, `AssemblyCompany`, `AssemblyVersion("0.1.3.0")`, `AssemblyFileVersion("0.1.3.0")`, and `AssemblyInformationalVersion("0.1.3-alpha")`.
- **TimeServiceManager Failure Caching (P2-9):** Cached query failures with a 5-second TTL in `GetSystemPeers`, eliminating repetitive process spawn overhead on failed `w32tm` queries.
- **Dead Code Elimination (I-1):** Removed unused P/Invoke declarations (`SetLocalTime`, `SetSystemTime`, `OpenProcessToken`, `LookupPrivilegeValue`, `TOKEN_PRIVILEGES`, `AdjustTokenPrivileges`, `CloseHandle`, `EnablePrivilege`, `SetSystemClock`) from `Win32Native`.

### Added
- **PowerShell Parity Test Suite:** Added `Tests/test_parity.ps1` validating CLI and C# Core parity across Era 0 / Era 1 timestamps, HTTP Date parsing, and error handling.
- **Comprehensive Unit Tests:** Expanded `Tests/UnitTests.cs` to 21 automated unit tests with strict `/warn:4 /warnaserror` compilation covering packet serialization, protocol rejection rules, 2036 rollover, process tree killing, unresolvable DNS bounds, and domain override seams.
- **Known Limitations Documentation:** Documented coarse HTTPS 1-second granularity, Active Directory time governance, and Group Policy execution policy precedence in `README.md` and `README.fa.md`.

### Changed
- **NTP Server Pool:** Expanded default global NTP consensus list to 5 Tier-1 servers (including `time.aws.com`).
- **Documentation Alignment:** Updated installation links to direct download endpoints and release assets, added `assets/` to repository tree, removed `SHA256SUMS.txt` from source tree, and synchronized Persian documentation (`README.fa.md`) section-for-section.

## [0.1.2] - 2026-09-19

### Changed
- **UI Menu Reorganization:** Demoted the RDR2 game fix button and menu entry to the bottom preset in both GUI (`Program.cs`) and CLI (`WinTime.ps1`) to emphasize general-purpose NTP synchronization.
- **CI Pre-Release Workflow:** Configured GitHub Actions release automation to publish alpha builds as pre-releases.

## [0.1.1] - 2026-09-19

- Initial alpha release of WinTime GUI and CLI.
