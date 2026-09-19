using System;
using System.Collections.Generic;
using System.ServiceProcess;
using Microsoft.Win32;

namespace WindowsTimeManager.Core
{
    public class SystemPeerInfo
    {
        public string Host { get; set; }
        public string Flag { get; set; }
        public bool IsActiveSource { get; set; }
        public string SourceType { get; set; }

        public override string ToString()
        {
            return string.Format("{0},{1} (Type: {2}{3})", Host, Flag, SourceType, IsActiveSource ? ", ACTIVE" : "");
        }
    }

    /// <summary>
    /// Manages the Windows Time Service (W32Time) safely and robustly.
    /// Eliminates false-success states by verifying exit codes of w32tm and sc commands,
    /// respects Active Directory domain time hierarchies, and removes unwarranted /reliable:yes flags.
    /// </summary>
    public static class TimeServiceManager
    {
        private const string W32TimeParamPath = @"SYSTEM\CurrentControlSet\Services\W32Time\Parameters";

        // In-memory cache for w32tm /query /source
        private static string _cachedActiveSource = null;
        private static DateTime _cacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
        private static readonly object _cacheLock = new object();

        // Testing seam for mocking domain join state
        public delegate bool DomainCheckDelegate(out string domainOrWorkgroupName);
        public static DomainCheckDelegate DomainCheckOverride = null;

        /// <summary>
        /// Clears the cached w32tm /query /source result.
        /// </summary>
        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedActiveSource = null;
                _cacheTimestamp = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Reads configured NTP peers along with current service sync Type (NTP, Nt5DS, NoSync, AllSync)
        /// and queries active source status from w32tm (using cached source if within 5s).
        /// </summary>
        public static List<SystemPeerInfo> GetSystemPeers(out string syncType, out string activeSource)
        {
            return GetSystemPeers(out syncType, out activeSource, false);
        }

        /// <summary>
        /// Reads configured NTP peers along with current service sync Type (NTP, Nt5DS, NoSync, AllSync)
        /// and queries active source status from w32tm with optional forceRefresh flag.
        /// </summary>
        public static List<SystemPeerInfo> GetSystemPeers(out string syncType, out string activeSource, bool forceRefresh)
        {
            List<SystemPeerInfo> list = new List<SystemPeerInfo>();
            syncType = "Unknown";
            activeSource = "Local CMOS Clock";

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(W32TimeParamPath))
                {
                    if (key != null)
                    {
                        object typeVal = key.GetValue("Type");
                        if (typeVal != null) syncType = typeVal.ToString().Trim();

                        object ntpVal = key.GetValue("NtpServer");
                        if (ntpVal != null)
                        {
                            string[] parts = ntpVal.ToString().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string p in parts)
                            {
                                PeerValidationResult val = PeerValidator.Validate(p);
                                if (val.IsValid)
                                {
                                    list.Add(new SystemPeerInfo
                                    {
                                        Host = val.CleanHost,
                                        Flag = val.Flag,
                                        SourceType = syncType,
                                        IsActiveSource = false
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Query active source from w32tm /query /source (with 5-second cache)
            string source = null;
            lock (_cacheLock)
            {
                if (!forceRefresh && _cachedActiveSource != null && (DateTime.UtcNow - _cacheTimestamp) < CacheDuration)
                {
                    source = _cachedActiveSource;
                }
            }

            if (source == null)
            {
                ProcessResult srcResult = ProcessRunner.Execute("w32tm.exe", "/query /source", timeoutMs: 3000);
                if (srcResult.Success && !string.IsNullOrEmpty(srcResult.StdOut))
                {
                    source = srcResult.StdOut.Trim();
                }
                else
                {
                    source = string.Empty; // Failure sentinel to avoid process spamming on error
                }

                lock (_cacheLock)
                {
                    _cachedActiveSource = source;
                    _cacheTimestamp = DateTime.UtcNow;
                }
            }

            if (!string.IsNullOrEmpty(source))
            {
                activeSource = source;
                foreach (SystemPeerInfo peer in list)
                {
                    if (activeSource.IndexOf(peer.Host, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        peer.IsActiveSource = true;
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Configures the manual peer list using w32tm.
        /// Does NOT specify /reliable:yes unless explicitly intended for time servers.
        /// Protects Active Directory domain synchronization unless override is allowed.
        /// </summary>
        public static bool SetSystemPeers(List<string> peerList, bool allowDomainOverride, out string message)
        {
            message = null;

            if (peerList == null || peerList.Count == 0)
            {
                message = "Peer list cannot be empty.";
                return false;
            }

            // Domain check: avoid silently corrupting enterprise domain hierarchy
            string domainName;
            bool isJoined = (DomainCheckOverride != null)
                ? DomainCheckOverride(out domainName)
                : NativeMethods.IsDomainJoined(out domainName);

            if (isJoined && !allowDomainOverride)
            {
                message = string.Format("System is joined to Active Directory domain '{0}'. Overriding manual NTP peers might disrupt domain time synchronization.", domainName);
                return false;
            }

            // Semantic validation of all input peers
            List<string> validatedPeers = new List<string>();
            foreach (string entry in peerList)
            {
                PeerValidationResult res = PeerValidator.Validate(entry);
                if (res.IsValid)
                {
                    validatedPeers.Add(res.FullEntry);
                }
            }

            if (validatedPeers.Count == 0)
            {
                message = "No valid peer entries found to apply.";
                return false;
            }

            string manualPeerString = string.Join(" ", validatedPeers.ToArray());

            // Ensure w32time service is configured for auto-start
            ProcessResult scResult = ProcessRunner.Execute("sc.exe", "config w32time start= auto", timeoutMs: 5000);
            if (!scResult.Success)
            {
                message = "Failed to configure w32time service startup: " + scResult.ErrorMessage;
                return false;
            }

            // Apply manual peer list using w32tm (omitting /reliable:yes for standard client workstations)
            string w32tmArgs = string.Format("/config /manualpeerlist:\"{0}\" /syncfromflags:manual /update", manualPeerString);
            ProcessResult w32Result = ProcessRunner.Execute("w32tm.exe", w32tmArgs, timeoutMs: 8000);
            if (!w32Result.Success)
            {
                message = "Failed to update Windows Time configuration: " + w32Result.ErrorMessage;
                return false;
            }

            // Verify w32time is running, start if stopped
            EnsureServiceRunning();

            InvalidateCache();

            message = string.Format("Successfully applied {0} NTP peer(s) to Windows Time Service.", validatedPeers.Count);
            return true;
        }

        /// <summary>
        /// Forces Windows Time Service to resynchronize with configured peers immediately.
        /// Strictly validates exit code and response text.
        /// </summary>
        public static bool Resync(out string details)
        {
            EnsureServiceRunning();

            ProcessResult result = ProcessRunner.Execute("w32tm.exe", "/resync /force", timeoutMs: 12000);
            if (result.Success && result.CombinedOutput.IndexOf("completed successfully", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                details = "The command completed successfully: Windows clock synchronized with system peers.";
                return true;
            }

            details = string.IsNullOrEmpty(result.CombinedOutput)
                ? "w32tm /resync command failed or timed out."
                : result.CombinedOutput;
            return false;
        }

        public static bool EnsureServiceRunning()
        {
            try
            {
                using (ServiceController sc = new ServiceController("w32time"))
                {
                    if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                    }
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                // Fallback via net start
                ProcessResult r = ProcessRunner.Execute("net.exe", "start w32time", timeoutMs: 5000);
                return r.Success;
            }
        }
    }
}
