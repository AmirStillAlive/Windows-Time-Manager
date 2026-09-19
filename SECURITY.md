# Security Policy

## Security Model & Threat Considerations

WinTime is a system utility designed for Windows clock synchronization, network time protocol (NTP) server management, and local system time manipulation. Because manipulating system time and system services requires privileged access, security is a core consideration of this project.

### 1. Privilege Separation & On-Demand Elevation
- **Invoker by Default (`asInvoker`):** The graphical application manifest runs with `asInvoker` privileges. Standard users can launch WinTime without triggering an initial User Account Control (UAC) prompt.
- **On-Demand Elevation:** Privileged operations (modifying Windows registry keys under `HKLM`, configuring the `w32time` service, and adjusting the hardware clock via `SetSystemTime`) request elevation only when initiated.
- **Strict Privilege Lifetime:** In C#, `SeSystemtimePrivilege` is acquired exclusively for the duration of the `SetSystemTime` Win32 call, immediately zeroed in a `finally` block, and token handles are closed.

### 2. Code Signing & Authenticode Status
- **Current Status:** Pre-compiled binaries and PowerShell scripts in the WinTime repository are **not currently signed with a trusted commercial Authenticode certificate**.
- **Motivation:** Trusted commercial code-signing certificates require annual third-party organizational verification. Until an official sponsor certificate is integrated, binaries and scripts remain unsigned.
- **Mitigation (Hash-Based Verification):** Every release publishes cryptographic SHA-256 checksums in `SHA256SUMS.txt`. The web installer (`install.ps1`) verifies the SHA-256 hash of all downloaded components prior to execution.

### 3. Execution Policy & Mark-of-the-Web (MOTW)
- Under default Windows PowerShell policies (`RemoteSigned`), downloaded `.ps1` files carrying the `Zone.Identifier` alternate data stream (ZoneId=3) are blocked from execution because they lack an Authenticode signature.
- To safely run WinTime without modifying machine-wide execution policies:
  - Use the bundled `WinTime.bat` launcher, which executes with `-NoProfile -ExecutionPolicy Bypass` and performs a local unblock, OR
  - Explicitly unblock the script: `Unblock-File -LiteralPath .\WinTime.ps1`, OR
  - Use the one-liner web installer: `irm <url> | iex` (runs directly in memory).

### 4. Active Directory Protection
- On machines joined to an Active Directory domain, manual reconfiguration of NTP peers can disrupt Kerberos authentication and domain trust.
- WinTime automatically detects domain membership and blocks peer overrides unless explicitly approved by the administrator.

## Verifying Release Integrity

To verify that your downloaded files have not been tampered with or corrupted during transit:

1. Download `SHA256SUMS.txt` from the official repository release or raw ref.
2. In PowerShell, compute the SHA-256 checksum:
   ```powershell
   Get-FileHash .\WinTime.exe, .\WinTime.ps1, .\WinTime.bat -Algorithm SHA256
   ```
3. Compare the resulting hash against `SHA256SUMS.txt`.

## Reporting a Vulnerability

If you discover a security vulnerability in WinTime, please report it responsibly:
- **Do not open a public GitHub issue** for undisclosed security vulnerabilities.
- Send an advisory via GitHub Security Advisories or contact the repository maintainers privately.
- Please provide detailed steps to reproduce the issue and any relevant system configuration details.
