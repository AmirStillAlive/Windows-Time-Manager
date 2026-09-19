using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Authentication;

namespace WindowsTimeManager.Core
{
    public class HttpsTimeResult
    {
        public bool Success { get; set; }
        public string Url { get; set; }
        public DateTime UtcTime { get; set; }
        public int LatencyMs { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsTlsClockSkewError { get; set; }

        public override string ToString()
        {
            if (!Success) return string.Format("{0}: Failed ({1})", Url, ErrorMessage);
            return string.Format("{0}: UTC={1:yyyy-MM-dd HH:mm:ss}, Latency={2}ms (HTTP Date header, 1s granularity)",
                Url, UtcTime, LatencyMs);
        }
    }

    /// <summary>
    /// Provides coarse-grained HTTP Date header synchronization when UDP port 123 is blocked.
    /// Explicitly documents non-atomic, 1-second precision, and diagnoses TLS certificate
    /// validation failures caused by severe system clock skew.
    /// </summary>
    public static class HttpsTimeClient
    {
        public static HttpsTimeResult QueryHttpDate(string url, int timeoutMs = 4000)
        {
            HttpsTimeResult result = new HttpsTimeResult
            {
                Url = url,
                Success = false
            };

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "HEAD";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; WinTime)";

                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                {
                    sw.Stop();
                    result.LatencyMs = (int)sw.ElapsedMilliseconds;

                    string dateHeader = res.Headers["Date"];
                    if (string.IsNullOrEmpty(dateHeader))
                    {
                        result.ErrorMessage = "HTTP response is missing 'Date' header.";
                        return result;
                    }

                    DateTime parsedUtc;
                    if (DateTime.TryParseExact(dateHeader, "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsedUtc))
                    {
                        // Add half RTT compensation (HTTP Date is 1-second coarse timestamp)
                        result.UtcTime = parsedUtc.AddMilliseconds(result.LatencyMs / 2.0);
                        result.Success = true;
                        return result;
                    }

                    result.ErrorMessage = "Failed to parse HTTP Date header format: " + dateHeader;
                    return result;
                }
            }
            catch (WebException wex)
            {
                sw.Stop();
                result.LatencyMs = (int)sw.ElapsedMilliseconds;

                // Inspect whether failure was caused by TLS certificate expiration / not yet valid
                string message = wex.Message;
                if (wex.Status == WebExceptionStatus.TrustFailure ||
                    wex.Status == WebExceptionStatus.SecureChannelFailure ||
                    (wex.InnerException is AuthenticationException) ||
                    message.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("secure channel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.IsTlsClockSkewError = true;
                    result.ErrorMessage = "TLS certificate validation failed. Your local system clock may be severely skewed (e.g. game preset) causing HTTPS certificates to appear invalid. Use standard SNTP (UDP 123) to resync.";
                }
                else if (wex.Status == WebExceptionStatus.Timeout)
                {
                    result.ErrorMessage = string.Format("HTTPS request to '{0}' timed out after {1} ms.", url, timeoutMs);
                }
                else
                {
                    result.ErrorMessage = "HTTP request failed: " + wex.Message;
                }
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.LatencyMs = (int)sw.ElapsedMilliseconds;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}
