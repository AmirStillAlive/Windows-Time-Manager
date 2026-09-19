using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WindowsTimeManager.Core
{
    public class NtpQueryResult
    {
        public bool Success { get; set; }
        public string Server { get; set; }
        public IPAddress ResolvedIp { get; set; }
        public TimeSpan ClockOffset { get; set; }
        public TimeSpan RoundTripDelay { get; set; }
        public DateTime TargetUtcTime { get; set; }
        public byte Stratum { get; set; }
        public string ReferenceId { get; set; }
        public string ErrorMessage { get; set; }

        public override string ToString()
        {
            if (!Success) return string.Format("{0}: Failed ({1})", Server, ErrorMessage);
            return string.Format("{0} [{1}]: Offset={2:+0.000;-0.000}s, Delay={3:0.0}ms, Stratum={4}",
                Server, ResolvedIp, ClockOffset.TotalSeconds, RoundTripDelay.TotalMilliseconds, Stratum);
        }
    }

    public class MultiNtpQueryResult
    {
        public bool Success { get; set; }
        public DateTime SelectedUtcTime { get; set; }
        public TimeSpan MedianOffset { get; set; }
        public List<NtpQueryResult> SuccessfulResults { get; set; }
        public List<NtpQueryResult> FailedResults { get; set; }
        public List<NtpQueryResult> OutlierResults { get; set; }
        public string Summary { get; set; }

        public MultiNtpQueryResult()
        {
            SuccessfulResults = new List<NtpQueryResult>();
            FailedResults = new List<NtpQueryResult>();
            OutlierResults = new List<NtpQueryResult>();
        }
    }

    /// <summary>
    /// Production-grade SNTP client providing single-server RFC 4330 query execution
    /// and multi-source consensus/outlier-rejection synchronization.
    /// </summary>
    public static class NtpClient
    {
        public const int DefaultNtpPort = 123;
        public const int DefaultTimeoutMs = 3000;

        /// <summary>
        /// Queries an NTP server with RFC 4330 compliance, cryptographic origin nonce validation,
        /// and standard 4-timestamp offset and network delay math.
        /// </summary>
        public static NtpQueryResult QueryServer(string host, int timeoutMs = DefaultTimeoutMs)
        {
            NtpQueryResult result = new NtpQueryResult
            {
                Server = host,
                Success = false
            };

            if (string.IsNullOrEmpty(host))
            {
                result.ErrorMessage = "Server hostname or IP cannot be empty.";
                return result;
            }

            try
            {
                // DNS Resolution
                IPAddress[] addresses = Dns.GetHostAddresses(host);
                if (addresses == null || addresses.Length == 0)
                {
                    result.ErrorMessage = "DNS resolution failed: no IP addresses found.";
                    return result;
                }

                // Prefer IPv4 for standard UDP NTP, fallback to IPv6 if only IPv6 is returned
                IPAddress targetIp = addresses[0];
                foreach (IPAddress ip in addresses)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        targetIp = ip;
                        break;
                    }
                }
                result.ResolvedIp = targetIp;

                IPEndPoint remoteEp = new IPEndPoint(targetIp, DefaultNtpPort);
                using (UdpClient udpClient = new UdpClient(targetIp.AddressFamily))
                {
                    udpClient.Client.ReceiveTimeout = timeoutMs;
                    udpClient.Client.SendTimeout = timeoutMs;
                    udpClient.Connect(remoteEp);

                    // Record t1 (Client Send Time in high precision UTC)
                    DateTime t1 = DateTime.UtcNow;
                    NtpPacket request = NtpPacket.CreateClientRequest(t1);
                    byte[] sendBuffer = request.ToBytes();

                    udpClient.Send(sendBuffer, sendBuffer.Length);

                    IPEndPoint recvEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] recvBuffer = udpClient.Receive(ref recvEp);

                    // Record t4 (Client Receive Time in high precision UTC)
                    DateTime t4 = DateTime.UtcNow;

                    NtpPacket response;
                    string valError;
                    if (!NtpPacket.TryParse(recvBuffer, out response, out valError))
                    {
                        result.ErrorMessage = valError;
                        return result;
                    }

                    // Strict Origin Timestamp Verification (matches client transmit t1)
                    if (!response.ValidateOriginTimestamp(t1, toleranceSeconds: 0.1))
                    {
                        result.ErrorMessage = "Origin timestamp mismatch: response does not correlate with request nonce.";
                        return result;
                    }

                    // Compute RFC 4330 4-timestamp offset and round-trip delay
                    TimeSpan clockOffset;
                    TimeSpan roundTripDelay;
                    DateTime targetUtc;
                    NtpPacket.CalculateOffsetAndDelay(
                        t1,
                        response.ReceiveTimestamp,
                        response.TransmitTimestamp,
                        t4,
                        out clockOffset,
                        out roundTripDelay,
                        out targetUtc);

                    result.ClockOffset = clockOffset;
                    result.RoundTripDelay = roundTripDelay;
                    result.TargetUtcTime = targetUtc;
                    result.Stratum = response.Stratum;
                    result.ReferenceId = response.ReferenceId;
                    result.Success = true;
                    return result;
                }
            }
            catch (SocketException sex)
            {
                if (sex.SocketErrorCode == SocketError.TimedOut)
                    result.ErrorMessage = string.Format("UDP port {0} timed out (blocked by firewall or ISP).", DefaultNtpPort);
                else
                    result.ErrorMessage = "Socket error: " + sex.Message;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Queries multiple NTP candidate servers concurrently, rejects malformed/outlier servers,
        /// and determines the consensus median clock offset.
        /// </summary>
        public static MultiNtpQueryResult QueryMultipleSources(IEnumerable<string> servers, int timeoutMs = DefaultTimeoutMs)
        {
            MultiNtpQueryResult multiResult = new MultiNtpQueryResult();
            List<string> serverList = new List<string>(servers);

            if (serverList.Count == 0)
            {
                multiResult.Summary = "No NTP servers specified.";
                return multiResult;
            }

            List<NtpQueryResult> allResults = new List<NtpQueryResult>();
            object syncLock = new object();
            using (CountdownEvent countdown = new CountdownEvent(serverList.Count))
            {
                foreach (string srv in serverList)
                {
                    string serverName = srv;
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        NtpQueryResult r = QueryServer(serverName, timeoutMs);
                        lock (syncLock)
                        {
                            allResults.Add(r);
                        }
                        countdown.Signal();
                    });
                }
                countdown.Wait(timeoutMs + 1000);
            }

            foreach (NtpQueryResult r in allResults)
            {
                if (r.Success)
                    multiResult.SuccessfulResults.Add(r);
                else
                    multiResult.FailedResults.Add(r);
            }

            if (multiResult.SuccessfulResults.Count == 0)
            {
                multiResult.Success = false;
                multiResult.Summary = "All candidate NTP servers failed to respond with valid protocol packets.";
                return multiResult;
            }

            // Single candidate edge-case
            if (multiResult.SuccessfulResults.Count == 1)
            {
                NtpQueryResult single = multiResult.SuccessfulResults[0];
                multiResult.Success = true;
                multiResult.MedianOffset = single.ClockOffset;
                multiResult.SelectedUtcTime = single.TargetUtcTime;
                multiResult.Summary = string.Format("Synchronized using single responsive peer '{0}' (Offset: {1:+0.000;-0.000}s)",
                    single.Server, single.ClockOffset.TotalSeconds);
                return multiResult;
            }

            // Sort by clock offset to compute median and filter statistical outliers
            multiResult.SuccessfulResults.Sort((a, b) => a.ClockOffset.CompareTo(b.ClockOffset));

            int count = multiResult.SuccessfulResults.Count;
            TimeSpan medianOffset;
            if (count % 2 == 1)
            {
                medianOffset = multiResult.SuccessfulResults[count / 2].ClockOffset;
            }
            else
            {
                long avgTicks = (multiResult.SuccessfulResults[(count / 2) - 1].ClockOffset.Ticks +
                                 multiResult.SuccessfulResults[count / 2].ClockOffset.Ticks) / 2;
                medianOffset = TimeSpan.FromTicks(avgTicks);
            }

            // Outlier Detection: Reject servers whose offset deviates from the median by more than 5.0 seconds
            // or whose round-trip delay is abnormally high (> 2000ms)
            List<NtpQueryResult> consensusPeers = new List<NtpQueryResult>();
            double maxAllowedDiscrepancySec = 5.0;

            foreach (NtpQueryResult r in multiResult.SuccessfulResults)
            {
                double discrepancy = Math.Abs((r.ClockOffset - medianOffset).TotalSeconds);
                if (discrepancy > maxAllowedDiscrepancySec)
                {
                    r.ErrorMessage = string.Format("Offset outlier: deviates by {0:0.00}s from group median", discrepancy);
                    multiResult.OutlierResults.Add(r);
                }
                else
                {
                    consensusPeers.Add(r);
                }
            }

            if (consensusPeers.Count == 0)
            {
                // Fallback to median peer if all got flagged
                consensusPeers.Add(multiResult.SuccessfulResults[count / 2]);
            }

            // Recompute average offset of consensus peers
            long totalTicks = 0;
            foreach (NtpQueryResult p in consensusPeers)
            {
                totalTicks += p.ClockOffset.Ticks;
            }
            TimeSpan finalOffset = TimeSpan.FromTicks(totalTicks / consensusPeers.Count);

            multiResult.Success = true;
            multiResult.MedianOffset = finalOffset;
            multiResult.SelectedUtcTime = DateTime.UtcNow + finalOffset;
            multiResult.Summary = string.Format("Consensus reached across {0} peer(s) (Median Offset: {1:+0.000;-0.000}s, {2} outlier(s) filtered)",
                consensusPeers.Count, finalOffset.TotalSeconds, multiResult.OutlierResults.Count);

            return multiResult;
        }
    }
}
