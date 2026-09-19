using System;
using System.Collections.Generic;
using System.Net;
using WindowsTimeManager.Core;

namespace WindowsTimeManager.Tests
{
    public static class UnitTests
    {
        private static int passed = 0;
        private static int failed = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("==============================================================");
            Console.WriteLine("               WINTIME CORE UNIT TEST SUITE                   ");
            Console.WriteLine("==============================================================");
            Console.WriteLine();

            RunTest("NTP Packet: Serialization & Wire Length", TestNtpPacket_SerializationAndLength);
            RunTest("NTP Packet: Rejection of Short / Malformed Packets", TestNtpPacket_RejectsMalformed);
            RunTest("NTP Packet: Rejection of Invalid Versions (VN 1, VN 5)", TestNtpPacket_RejectsInvalidVersion);
            RunTest("NTP Packet: Rejection of Invalid Modes (Mode 3, Mode 0)", TestNtpPacket_RejectsInvalidMode);
            RunTest("NTP Packet: Rejection of Alarm Condition (LI = 3)", TestNtpPacket_RejectsLeapIndicator3);
            RunTest("NTP Packet: Rejection of Kiss-o'-Death (Stratum 0, RATE)", TestNtpPacket_RejectsKissOfDeath);
            RunTest("NTP Packet: Rejection of Unsynchronized Server (Stratum 16)", TestNtpPacket_RejectsUnsynchronizedStratum);
            RunTest("NTP Packet: Origin Nonce / Timestamp Verification", TestNtpPacket_OriginNonceVerification);
            RunTest("SNTP Math: Standard 4-Timestamp Offset & Delay Calculation", TestSntpMath_OffsetAndDelay);
            RunTest("Peer Validator: Valid Public IPv4 Addresses", TestPeerValidator_ValidIpv4);
            RunTest("Peer Validator: Rejection of IPv4 Loopback (127.0.0.0/8 & localhost)", TestPeerValidator_RejectLoopback);
            RunTest("Peer Validator: Rejection of Broadcast, Any, and Multicast", TestPeerValidator_RejectBroadcastAndMulticast);
            RunTest("Peer Validator: Valid Public IPv6 Addresses", TestPeerValidator_ValidIpv6);
            RunTest("Peer Validator: Rejection of IPv6 Loopback (::1) and Multicast", TestPeerValidator_RejectIpv6LoopbackAndMulticast);
            RunTest("Peer Validator: FQDN Domain Validation Rules (hyphens, dots, TLD)", TestPeerValidator_HostnameRules);
            RunTest("Process Runner: Strict Exit Code & False-Success Elimination", TestProcessRunner_ExitCode);
            RunTest("NTP Packet: 2036 Rollover Era-Inference Heuristic", TestNtpPacket_Rollover2036);
            RunTest("HTTPS Time: RFC 1123, RFC 850, and ANSI C Date Parsing", TestHttpsTimeClient_ParseHttpDate);
            RunTest("Process Runner: Process Timeout and Clean Process Kill", TestProcessRunner_TimeoutAndKill);
            RunTest("Time Service: Domain Policy Blocking and Override Flag", TestTimeServiceManager_DomainPolicyAndOverride);

            Console.WriteLine();
            Console.WriteLine("==============================================================");
            Console.WriteLine(string.Format("RESULTS: {0} Passed, {1} Failed", passed, failed));
            Console.WriteLine("==============================================================");

            return failed == 0 ? 0 : 1;
        }

        private static void RunTest(string name, Action testMethod)
        {
            Console.Write("  [*] " + name.PadRight(62) + " ");
            try
            {
                testMethod();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PASS]");
                Console.ResetColor();
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAIL]");
                Console.WriteLine("      Error: " + ex.Message);
                Console.ResetColor();
                failed++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        #region NTP Protocol & Packet Tests

        private static void TestNtpPacket_SerializationAndLength()
        {
            DateTime t1 = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);
            NtpPacket req = NtpPacket.CreateClientRequest(t1);
            byte[] wire = req.ToBytes();

            Assert(wire != null && wire.Length == 48, "Packet length must be exactly 48 bytes");
            Assert((wire[0] & 0x07) == 3, "Client mode must be 3");
            Assert(((wire[0] >> 3) & 0x07) == 4, "Version must be 4");

            DateTime readT1 = NtpPacket.ReadTimestamp(wire, 40);
            double diff = Math.Abs((readT1 - t1).TotalSeconds);
            Assert(diff < 0.001, "Serialized transmit timestamp must match original");
        }

        private static void TestNtpPacket_RejectsMalformed()
        {
            byte[] empty = new byte[0];
            byte[] shortBuf = new byte[32];
            NtpPacket p;
            string err;

            Assert(!NtpPacket.TryParse(empty, out p, out err), "Empty packet must be rejected");
            Assert(!NtpPacket.TryParse(shortBuf, out p, out err), "Short packet (< 48 bytes) must be rejected");
            Assert(!NtpPacket.TryParse(null, out p, out err), "Null packet must be rejected");
        }

        private static void TestNtpPacket_RejectsInvalidVersion()
        {
            byte[] buf = CreateValidMockResponse();
            buf[0] = (byte)((buf[0] & 0xC7) | (1 << 3)); // VN = 1
            NtpPacket p;
            string err;
            Assert(!NtpPacket.TryParse(buf, out p, out err), "Version 1 must be rejected");

            buf[0] = (byte)((buf[0] & 0xC7) | (5 << 3)); // VN = 5
            Assert(!NtpPacket.TryParse(buf, out p, out err), "Version 5 must be rejected");
        }

        private static void TestNtpPacket_RejectsInvalidMode()
        {
            byte[] buf = CreateValidMockResponse();
            buf[0] = (byte)((buf[0] & 0xF8) | 3); // Mode 3 (Client)
            NtpPacket p;
            string err;
            Assert(!NtpPacket.TryParse(buf, out p, out err), "Mode 3 in response must be rejected");

            buf[0] = (byte)((buf[0] & 0xF8) | 0); // Mode 0 (Reserved)
            Assert(!NtpPacket.TryParse(buf, out p, out err), "Mode 0 in response must be rejected");
        }

        private static void TestNtpPacket_RejectsLeapIndicator3()
        {
            byte[] buf = CreateValidMockResponse();
            buf[0] = (byte)((buf[0] & 0x3F) | (3 << 6)); // LI = 3 (Alarm / Unsynchronized)
            NtpPacket p;
            string err;
            Assert(!NtpPacket.TryParse(buf, out p, out err), "LI = 3 must be rejected as unsynchronized alarm");
        }

        private static void TestNtpPacket_RejectsKissOfDeath()
        {
            byte[] buf = CreateValidMockResponse();
            buf[1] = 0; // Stratum 0 (Kiss-o'-Death)
            buf[12] = (byte)'R'; buf[13] = (byte)'A'; buf[14] = (byte)'T'; buf[15] = (byte)'E';
            NtpPacket p;
            string err;
            bool ok = NtpPacket.TryParse(buf, out p, out err);
            Assert(!ok, "Stratum 0 (KoD) must be rejected");
            Assert(err.Contains("Kiss-o'-Death") && err.Contains("RATE"), "Error message must report KoD code RATE");
        }

        private static void TestNtpPacket_RejectsUnsynchronizedStratum()
        {
            byte[] buf = CreateValidMockResponse();
            buf[1] = 16; // Stratum 16
            NtpPacket p;
            string err;
            Assert(!NtpPacket.TryParse(buf, out p, out err), "Stratum 16 must be rejected");
        }

        private static void TestNtpPacket_OriginNonceVerification()
        {
            byte[] buf = CreateValidMockResponse();
            DateTime t1 = new DateTime(2026, 9, 19, 15, 30, 0, DateTimeKind.Utc);
            NtpPacket.WriteTimestamp(buf, 24, t1); // Write t1 to Origin Timestamp

            NtpPacket p;
            string err;
            Assert(NtpPacket.TryParse(buf, out p, out err), "Valid response buffer must parse");

            Assert(p.ValidateOriginTimestamp(t1), "Origin timestamp matching t1 must pass");
            Assert(!p.ValidateOriginTimestamp(t1.AddSeconds(10)), "Mismatched origin timestamp must fail validation");
        }

        private static void TestSntpMath_OffsetAndDelay()
        {
            // Standard RFC 4330 SNTP calculation test vector:
            // t1 (client send)    = 10.000s
            // t2 (server receive) = 10.100s  (server is ahead by 50ms + 50ms transit)
            // t3 (server xmit)    = 10.105s  (server processing took 5ms)
            // t4 (client receive) = 10.215s  (client receives at 10.215s)
            //
            // Total round-trip delay theta = (t4 - t1) - (t3 - t2) = (0.215) - (0.005) = 0.210s = 210ms
            // Clock offset delta = ((t2 - t1) + (t3 - t4)) / 2 = ((0.100) + (-0.110)) / 2 = -0.010 / 2 = -0.005s = -5ms

            DateTime baseTime = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);
            DateTime t1 = baseTime.AddSeconds(10.000);
            DateTime t2 = baseTime.AddSeconds(10.100);
            DateTime t3 = baseTime.AddSeconds(10.105);
            DateTime t4 = baseTime.AddSeconds(10.215);

            TimeSpan offset;
            TimeSpan delay;
            DateTime target;

            NtpPacket.CalculateOffsetAndDelay(t1, t2, t3, t4, out offset, out delay, out target);

            double expectedOffsetMs = -5.0;
            double expectedDelayMs = 210.0;

            Assert(Math.Abs(offset.TotalMilliseconds - expectedOffsetMs) < 0.1,
                string.Format("Offset calculation error: expected {0}ms, got {1}ms", expectedOffsetMs, offset.TotalMilliseconds));
            Assert(Math.Abs(delay.TotalMilliseconds - expectedDelayMs) < 0.1,
                string.Format("Delay calculation error: expected {0}ms, got {1}ms", expectedDelayMs, delay.TotalMilliseconds));

            DateTime expectedTarget = t4 + offset;
            Assert(target == expectedTarget, "Target UTC must equal t4 + offset");
        }

        private static byte[] CreateValidMockResponse()
        {
            byte[] buf = new byte[48];
            buf[0] = (byte)((0 << 6) | (4 << 3) | 4); // LI=0, VN=4, Mode=4 (Server)
            buf[1] = 1; // Stratum 1
            buf[2] = 6; // Poll
            buf[3] = unchecked((byte)-20); // Precision

            DateTime now = DateTime.UtcNow;
            NtpPacket.WriteTimestamp(buf, 16, now.AddSeconds(-60)); // Ref
            NtpPacket.WriteTimestamp(buf, 24, now.AddSeconds(-1));  // Origin
            NtpPacket.WriteTimestamp(buf, 32, now.AddSeconds(-0.5));// Recv
            NtpPacket.WriteTimestamp(buf, 40, now);                 // Xmit

            return buf;
        }

        #endregion

        #region Peer Validation Tests

        private static void TestPeerValidator_ValidIpv4()
        {
            PeerValidationResult r1 = PeerValidator.Validate("1.1.1.1");
            Assert(r1.IsValid && r1.CleanHost == "1.1.1.1" && r1.FullEntry == "1.1.1.1,0x8", "1.1.1.1 must be valid");

            PeerValidationResult r2 = PeerValidator.Validate("8.8.8.8,0x1");
            Assert(r2.IsValid && r2.Flag == "0x1" && r2.FullEntry == "8.8.8.8,0x1", "8.8.8.8 with flag 0x1 must be valid");
        }

        private static void TestPeerValidator_RejectLoopback()
        {
            Assert(!PeerValidator.Validate("127.0.0.1").IsValid, "127.0.0.1 must be rejected");
            Assert(!PeerValidator.Validate("127.10.20.30").IsValid, "127.x.x.x must be rejected");
            Assert(!PeerValidator.Validate("localhost").IsValid, "'localhost' must be rejected");
        }

        private static void TestPeerValidator_RejectBroadcastAndMulticast()
        {
            Assert(!PeerValidator.Validate("255.255.255.255").IsValid, "255.255.255.255 must be rejected");
            Assert(!PeerValidator.Validate("0.0.0.0").IsValid, "0.0.0.0 must be rejected");
            Assert(!PeerValidator.Validate("224.0.1.1").IsValid, "Multicast 224.0.1.1 must be rejected");
            Assert(!PeerValidator.Validate("239.255.255.250").IsValid, "Multicast 239.255.255.250 must be rejected");
        }

        private static void TestPeerValidator_ValidIpv6()
        {
            PeerValidationResult r = PeerValidator.Validate("2001:4860:4860::8888");
            Assert(r.IsValid, "Google IPv6 DNS/NTP address must be valid");
        }

        private static void TestPeerValidator_RejectIpv6LoopbackAndMulticast()
        {
            Assert(!PeerValidator.Validate("::1").IsValid, "IPv6 ::1 loopback must be rejected");
            Assert(!PeerValidator.Validate("::").IsValid, "IPv6 :: unspecified must be rejected");
            Assert(!PeerValidator.Validate("ff02::1").IsValid, "IPv6 multicast ff02::1 must be rejected");
        }

        private static void TestPeerValidator_HostnameRules()
        {
            Assert(PeerValidator.Validate("time.google.com").IsValid, "time.google.com must be valid");
            Assert(PeerValidator.Validate("pool.ntp.org,0x8").IsValid, "pool.ntp.org,0x8 must be valid");
            Assert(PeerValidator.Validate("time.cloudflare.com").IsValid, "time.cloudflare.com must be valid");

            Assert(!PeerValidator.Validate("").IsValid, "Empty hostname must be rejected");
            Assert(!PeerValidator.Validate("singlelabel").IsValid, "Single-label hostname without domain dot must be rejected");
            Assert(!PeerValidator.Validate("-leadinghyphen.com").IsValid, "Leading hyphen in domain label must be rejected");
            Assert(!PeerValidator.Validate("trailinghyphen-.com").IsValid, "Trailing hyphen in domain label must be rejected");
            Assert(!PeerValidator.Validate("bad..domain.com").IsValid, "Consecutive dots must be rejected");
            Assert(!PeerValidator.Validate("domain.123").IsValid, "All-numeric TLD must be rejected");
            Assert(!PeerValidator.Validate("host with spaces.com").IsValid, "Spaces in hostname must be rejected");
            Assert(!PeerValidator.Validate("host;injection.com").IsValid, "Semicolons must be rejected");
        }

        #endregion

        #region Process Runner Tests

        private static void TestProcessRunner_ExitCode()
        {
            // Test exit 0
            ProcessResult r0 = ProcessRunner.Execute("cmd.exe", "/c exit 0", timeoutMs: 3000);
            Assert(r0.Success && r0.ExitCode == 0, "cmd exit 0 must report Success = true");

            // Test exit 42
            ProcessResult r42 = ProcessRunner.Execute("cmd.exe", "/c exit 42", timeoutMs: 3000);
            Assert(!r42.Success && r42.ExitCode == 42, "cmd exit 42 must report Success = false and ExitCode = 42");

            // Test non-existent executable
            ProcessResult rBad = ProcessRunner.Execute("non_existent_command_xyz_123.exe", "", timeoutMs: 1000);
            Assert(!rBad.Success, "Non-existent command must report Success = false");
        }

        private static void TestProcessRunner_TimeoutAndKill()
        {
            // Ping 127.0.0.1 10 times takes ~9-10 seconds. Timeout is set to 400ms.
            DateTime start = DateTime.UtcNow;
            ProcessResult res = ProcessRunner.Execute("cmd.exe", "/c ping 127.0.0.1 -n 10", timeoutMs: 400);
            TimeSpan elapsed = DateTime.UtcNow - start;

            Assert(res.TimedOut, "ProcessRunner must flag TimedOut = true when execution exceeds timeout");
            Assert(!res.Success, "Timed-out process must report Success = false");
            Assert(res.ErrorMessage != null && res.ErrorMessage.Contains("timed out"), "Error message must report timeout");
            Assert(elapsed.TotalSeconds < 5.0, "Process must be terminated promptly and not hang");
        }

        #endregion

        #region Extended Protocol, HTTP & Integration Tests

        private static void TestNtpPacket_Rollover2036()
        {
            byte[] buf = new byte[8];

            // Era 0: current epoch (e.g. year 2026, intPart >= 0x80000000UL)
            DateTime era0Date = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);
            NtpPacket.WriteTimestamp(buf, 0, era0Date);
            DateTime readEra0 = NtpPacket.ReadTimestamp(buf, 0);
            Assert(readEra0.Year == 2026, "Era 0 timestamp year must be 2026");
            Assert(Math.Abs((readEra0 - era0Date).TotalSeconds) < 0.001, "Era 0 timestamp must match original within 1ms");

            // Era 1: post-2036 epoch (e.g. year 2038, intPart < 0x80000000UL)
            DateTime era1Date = new DateTime(2038, 5, 10, 8, 30, 0, DateTimeKind.Utc);
            NtpPacket.WriteTimestamp(buf, 0, era1Date);
            DateTime readEra1 = NtpPacket.ReadTimestamp(buf, 0);
            Assert(readEra1.Year == 2038, "Era 1 timestamp year must be 2038");
            Assert(Math.Abs((readEra1 - era1Date).TotalSeconds) < 0.001, "Era 1 timestamp must match original within 1ms");
        }

        private static void TestHttpsTimeClient_ParseHttpDate()
        {
            DateTime dt;

            // RFC 1123 standard format
            Assert(HttpsTimeClient.TryParseHttpDate("Sun, 06 Nov 1994 08:49:37 GMT", out dt), "RFC 1123 date must parse");
            Assert(dt.Year == 1994 && dt.Month == 11 && dt.Day == 6 && dt.Hour == 8 && dt.Minute == 49 && dt.Second == 37, "RFC 1123 fields must match");
            Assert(dt.Kind == DateTimeKind.Utc, "Parsed date must be UTC");

            // RFC 850 legacy format
            Assert(HttpsTimeClient.TryParseHttpDate("Sunday, 06-Nov-94 08:49:37 GMT", out dt), "RFC 850 date must parse");
            Assert(dt.Year == 1994 && dt.Month == 11 && dt.Day == 6 && dt.Hour == 8 && dt.Minute == 49 && dt.Second == 37, "RFC 850 fields must match");

            // ANSI C asctime() format
            Assert(HttpsTimeClient.TryParseHttpDate("Sun Nov  6 08:49:37 1994", out dt), "ANSI C asctime date must parse");
            Assert(dt.Year == 1994 && dt.Month == 11 && dt.Day == 6 && dt.Hour == 8 && dt.Minute == 49 && dt.Second == 37, "ANSI C fields must match");

            // Invalid formats
            Assert(!HttpsTimeClient.TryParseHttpDate("invalid-date-string", out dt), "Garbage date string must return false");
            Assert(!HttpsTimeClient.TryParseHttpDate(null, out dt), "Null date string must return false");
            Assert(!HttpsTimeClient.TryParseHttpDate("", out dt), "Empty date string must return false");
        }

        private static void TestTimeServiceManager_DomainPolicyAndOverride()
        {
            try
            {
                TimeServiceManager.DomainCheckOverride = delegate(out string domain)
                {
                    domain = "CORP.CONTOSO.COM";
                    return true;
                };

                string message;
                List<string> testPeers = new List<string> { "time.google.com" };

                // Should be blocked when allowDomainOverride is false
                bool blocked = !TimeServiceManager.SetSystemPeers(testPeers, allowDomainOverride: false, message: out message);
                Assert(blocked, "Domain-joined system must block NTP reconfiguration when allowDomainOverride is false");
                Assert(message != null && message.Contains("CORP.CONTOSO.COM"), "Block message must specify domain name");

                // When allowDomainOverride is true, domain check is bypassed.
                // We pass invalid peers to verify that domain check passed and reached peer validation step.
                List<string> invalidPeers = new List<string> { "invalid_domain_test" };
                bool overrideAllowed = !TimeServiceManager.SetSystemPeers(invalidPeers, allowDomainOverride: true, message: out message);
                Assert(overrideAllowed, "Domain override must allow execution to proceed past domain check");
                Assert(message != null && message.Contains("No valid peer entries found"), "Domain check must pass and fail on peer validation instead");
            }
            finally
            {
                TimeServiceManager.DomainCheckOverride = null;
            }
        }

        #endregion
    }
}
