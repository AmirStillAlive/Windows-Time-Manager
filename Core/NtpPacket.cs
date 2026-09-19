using System;
using System.Text;

namespace WindowsTimeManager.Core
{
    /// <summary>
    /// Implements RFC 4330 / RFC 5905 Simple Network Time Protocol (SNTP) Packet Specification.
    /// Handles binary packet layout, 64-bit fixed-point timestamp serialization,
    /// protocol-level security checks (Origin Nonce validation, Kiss-o'-Death, Leap Indicator),
    /// and standard 4-timestamp offset and round-trip delay computation.
    /// </summary>
    public class NtpPacket
    {
        public const int PacketSize = 48;
        public static readonly DateTime NtpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly DateTime MinPlausibleServerTime = NtpEpoch.AddYears(120); // 2020-01-01 00:00:00 UTC

        public byte LeapIndicator { get; set; }
        public byte VersionNumber { get; set; }
        public byte Mode { get; set; }
        public byte Stratum { get; set; }
        public sbyte PollInterval { get; set; }
        public sbyte Precision { get; set; }
        public double RootDelayMs { get; set; }
        public double RootDispersionMs { get; set; }
        public string ReferenceId { get; set; }

        public DateTime ReferenceTimestamp { get; set; }
        public DateTime OriginTimestamp { get; set; }
        public DateTime ReceiveTimestamp { get; set; }
        public DateTime TransmitTimestamp { get; set; }

        public byte[] RawBytes { get; private set; }

        public NtpPacket()
        {
            VersionNumber = 4;
            Mode = 3; // Client request
            LeapIndicator = 0;
            ReferenceId = string.Empty;
        }

        /// <summary>
        /// Creates a client SNTP request packet with high-resolution transmit timestamp (acting as a nonce).
        /// </summary>
        public static NtpPacket CreateClientRequest(DateTime clientTransmitUtc)
        {
            NtpPacket packet = new NtpPacket
            {
                VersionNumber = 4,
                Mode = 3, // Client
                LeapIndicator = 0,
                Stratum = 0,
                PollInterval = 0,
                Precision = 0,
                TransmitTimestamp = clientTransmitUtc
            };
            return packet;
        }

        /// <summary>
        /// Serializes this packet into a 48-byte NTP wire buffer.
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] buffer = new byte[PacketSize];
            buffer[0] = (byte)(((LeapIndicator & 0x03) << 6) | ((VersionNumber & 0x07) << 3) | (Mode & 0x07));
            buffer[1] = Stratum;
            buffer[2] = (byte)PollInterval;
            buffer[3] = (byte)Precision;

            // Transmit timestamp at offset 40
            WriteTimestamp(buffer, 40, TransmitTimestamp);
            RawBytes = buffer;
            return buffer;
        }

        /// <summary>
        /// Parses a 48-byte NTP response buffer and validates protocol fields.
        /// </summary>
        public static bool TryParse(byte[] buffer, out NtpPacket packet, out string validationError)
        {
            packet = null;
            validationError = null;

            if (buffer == null || buffer.Length < PacketSize)
            {
                validationError = string.Format("Malformed NTP packet: expected at least {0} bytes, got {1}",
                    PacketSize, buffer == null ? 0 : buffer.Length);
                return false;
            }

            NtpPacket p = new NtpPacket
            {
                RawBytes = (byte[])buffer.Clone(),
                LeapIndicator = (byte)((buffer[0] >> 6) & 0x03),
                VersionNumber = (byte)((buffer[0] >> 3) & 0x07),
                Mode = (byte)(buffer[0] & 0x07),
                Stratum = buffer[1],
                PollInterval = (sbyte)buffer[2],
                Precision = (sbyte)buffer[3]
            };

            // Protocol Check: Version must be 3 or 4
            if (p.VersionNumber != 3 && p.VersionNumber != 4)
            {
                validationError = string.Format("Invalid NTP version: {0}. Supported versions: 3, 4", p.VersionNumber);
                return false;
            }

            // Protocol Check: Mode must be Server (4) or Broadcast (5)
            if (p.Mode != 4 && p.Mode != 5)
            {
                validationError = string.Format("Invalid NTP mode: {0}. Expected 4 (Server) or 5 (Broadcast)", p.Mode);
                return false;
            }

            // Protocol Check: Leap Indicator 3 indicates alarm condition (clock not synchronized)
            if (p.LeapIndicator == 3)
            {
                validationError = "Server reports alarm condition: clock is unsynchronized (Leap Indicator = 3)";
                return false;
            }

            // Reference Identifier extraction
            p.ReferenceId = ExtractReferenceId(buffer, p.Stratum);

            // Protocol Check: Stratum 0 indicates Kiss-o'-Death packet
            if (p.Stratum == 0)
            {
                validationError = string.Format("Server returned Kiss-o'-Death packet (KoD Code: '{0}'). Client rate-limited or denied.", p.ReferenceId);
                return false;
            }

            // Protocol Check: Stratum > 15 indicates unsynchronized server
            if (p.Stratum > 15)
            {
                validationError = string.Format("Server stratum is {0}, which is unsynchronized (maximum valid stratum is 15)", p.Stratum);
                return false;
            }

            // Parse timestamps
            p.ReferenceTimestamp = ReadTimestamp(buffer, 16);
            p.OriginTimestamp = ReadTimestamp(buffer, 24);
            p.ReceiveTimestamp = ReadTimestamp(buffer, 32);
            p.TransmitTimestamp = ReadTimestamp(buffer, 40);

            // Protocol Check: Transmit timestamp cannot be zero or implausible
            if (p.TransmitTimestamp == DateTime.MinValue || p.TransmitTimestamp <= MinPlausibleServerTime)
            {
                validationError = string.Format("Server transmit timestamp is invalid or before {0:yyyy-MM-dd} (unsynchronized server response)", MinPlausibleServerTime);
                return false;
            }

            // Protocol Check: Receive timestamp cannot be zero in standard unicast mode
            if (p.ReceiveTimestamp == DateTime.MinValue || p.ReceiveTimestamp <= MinPlausibleServerTime)
            {
                validationError = string.Format("Server receive timestamp is invalid or before {0:yyyy-MM-dd}", MinPlausibleServerTime);
                return false;
            }

            packet = p;
            return true;
        }

        /// <summary>
        /// Validates that the server response matches the exact client request (Origin Timestamp check).
        /// Protects against off-path packet injection and replay attacks.
        /// </summary>
        public bool ValidateOriginTimestamp(DateTime clientTransmitUtc, double toleranceSeconds = 0.05)
        {
            if (OriginTimestamp == DateTime.MinValue)
                return false;

            double diff = Math.Abs((OriginTimestamp - clientTransmitUtc).TotalSeconds);
            return diff <= toleranceSeconds;
        }

        /// <summary>
        /// Computes standard SNTP clock offset and round-trip network delay using RFC 4330 four-timestamp math.
        /// t1: Client request departure time
        /// t2: Server request arrival time
        /// t3: Server response departure time
        /// t4: Client response arrival time
        /// Offset = ((t2 - t1) + (t3 - t4)) / 2
        /// Delay  = (t4 - t1) - (t3 - t2)
        /// Target Synchronized UTC = t4 + Offset
        /// </summary>
        public static void CalculateOffsetAndDelay(
            DateTime t1ClientSendUtc,
            DateTime t2ServerRecvUtc,
            DateTime t3ServerXmitUtc,
            DateTime t4ClientRecvUtc,
            out TimeSpan clockOffset,
            out TimeSpan roundTripDelay,
            out DateTime targetUtcTime)
        {
            TimeSpan t2_minus_t1 = t2ServerRecvUtc - t1ClientSendUtc;
            TimeSpan t3_minus_t4 = t3ServerXmitUtc - t4ClientRecvUtc;

            long offsetTicks = (t2_minus_t1.Ticks + t3_minus_t4.Ticks) / 2;
            clockOffset = TimeSpan.FromTicks(offsetTicks);

            TimeSpan t4_minus_t1 = t4ClientRecvUtc - t1ClientSendUtc;
            TimeSpan t3_minus_t2 = t3ServerXmitUtc - t2ServerRecvUtc;

            long delayTicks = t4_minus_t1.Ticks - t3_minus_t2.Ticks;
            if (delayTicks < 0) delayTicks = 0; // Negative delay clamp
            roundTripDelay = TimeSpan.FromTicks(delayTicks);

            targetUtcTime = t4ClientRecvUtc + clockOffset;
        }

        #region Binary Timestamp Serialization Helpers

        /// <summary>
        /// Writes an RFC 4330 64-bit fixed-point timestamp (32-bit seconds, 32-bit fraction).
        /// Note: The 32-bit seconds field counts seconds since 1900-01-01 00:00:00 UTC and
        /// overflows on 2036-02-07 06:28:16 UTC (NTP Era 0 to Era 1 rollover). Timestamps
        /// beyond this rollover wrap around modulo 2^32 into Era 1.
        /// </summary>
        public static void WriteTimestamp(byte[] buffer, int offset, DateTime utcTime)
        {
            if (utcTime == DateTime.MinValue || utcTime <= NtpEpoch)
            {
                for (int i = 0; i < 8; i++) buffer[offset + i] = 0;
                return;
            }

            TimeSpan span = utcTime - NtpEpoch;
            ulong rawTotalSeconds = (ulong)Math.Max(0, span.TotalSeconds);
            // Modulo 2^32 for Era 1 (after 2036 rollover)
            ulong totalSeconds = rawTotalSeconds & 0xFFFFFFFFUL;
            double fractionalSeconds = span.TotalSeconds - (double)rawTotalSeconds;
            ulong fraction = (ulong)(fractionalSeconds * 4294967296.0); // 2^32

            buffer[offset + 0] = (byte)((totalSeconds >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((totalSeconds >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((totalSeconds >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(totalSeconds & 0xFF);

            buffer[offset + 4] = (byte)((fraction >> 24) & 0xFF);
            buffer[offset + 5] = (byte)((fraction >> 16) & 0xFF);
            buffer[offset + 6] = (byte)((fraction >> 8) & 0xFF);
            buffer[offset + 7] = (byte)(fraction & 0xFF);
        }

        /// <summary>
        /// Reads an RFC 4330 64-bit fixed-point timestamp (32-bit seconds, 32-bit fraction).
        /// Protocol Limitation & Era-Inference Heuristic (RFC 4330 Section 3 / RFC 5905):
        /// The standard NTP 32-bit unsigned seconds field overflows on 2036-02-07 06:28:16 UTC.
        /// Values with MSB = 1 (intPart >= 0x80000000) correspond to years 1968–2036 (Era 0).
        /// Values with MSB = 0 (intPart < 0x80000000), which would otherwise represent years
        /// 1900–1968 before modern NTP existed, are unambiguously inferred to belong to Era 1
        /// (spanning 2036-02-07 to 2172-03-16 UTC).
        /// </summary>
        public static DateTime ReadTimestamp(byte[] buffer, int offset)
        {
            ulong intPart = ((ulong)buffer[offset] << 24) |
                            ((ulong)buffer[offset + 1] << 16) |
                            ((ulong)buffer[offset + 2] << 8) |
                            (ulong)buffer[offset + 3];

            ulong fractPart = ((ulong)buffer[offset + 4] << 24) |
                              ((ulong)buffer[offset + 5] << 16) |
                              ((ulong)buffer[offset + 6] << 8) |
                              (ulong)buffer[offset + 7];

            if (intPart == 0 && fractPart == 0)
                return DateTime.MinValue;

            // Apply standard era-inference heuristic: if intPart < 0x80000000, infer Era 1 (add 2^32 seconds)
            ulong secondsWithEra = (intPart < 0x80000000UL) ? (intPart + 4294967296UL) : intPart;

            try
            {
                long ticks = (long)(secondsWithEra * (ulong)TimeSpan.TicksPerSecond) +
                             (long)((fractPart * (double)TimeSpan.TicksPerSecond) / 4294967296.0);
                return NtpEpoch.AddTicks(ticks);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static string ExtractReferenceId(byte[] buffer, byte stratum)
        {
            if (stratum == 0 || stratum == 1)
            {
                // 4-character ASCII identifier
                StringBuilder sb = new StringBuilder();
                for (int i = 12; i < 16; i++)
                {
                    char c = (char)buffer[i];
                    if (c >= 32 && c <= 126) sb.Append(c);
                }
                return sb.ToString().Trim();
            }
            // IPv4 reference or MD5 hash of IPv6
            return string.Format("{0}.{1}.{2}.{3}", buffer[12], buffer[13], buffer[14], buffer[15]);
        }

        #endregion
    }
}
