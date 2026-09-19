using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;

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
    /// Uses HttpClient with explicit TLS 1.2/1.3 configuration and multi-format HTTP Date parsing.
    /// </summary>
    public static class HttpsTimeClient
    {
        public static readonly string[] HttpDateFormats = new string[]
        {
            "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
            "ddd, dd MMM yyyy HH:mm:ss GMT",
            "dddd, dd-MMM-yy HH:mm:ss 'GMT'",
            "dddd, dd-MMM-yy HH:mm:ss GMT",
            "ddd MMM d HH:mm:ss yyyy",
            "ddd MMM  d HH:mm:ss yyyy",
            "ddd MMM dd HH:mm:ss yyyy",
            "r"
        };

        static HttpsTimeClient()
        {
            EnforceModernTls();
        }

        /// <summary>
        /// Explicitly enforces TLS 1.2 and TLS 1.3 protocol support.
        /// </summary>
        public static void EnforceModernTls()
        {
            try
            {
                // 12288 = Tls13 enum value in .NET Framework 4.8 / .NET Core
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
            }
            catch
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }
                catch { }
            }
        }

        /// <summary>
        /// Parses an HTTP Date header value supporting RFC 1123, RFC 850, and ANSI C asctime formats.
        /// </summary>
        public static bool TryParseHttpDate(string dateString, out DateTime parsedUtc)
        {
            parsedUtc = DateTime.MinValue;
            if (string.IsNullOrEmpty(dateString)) return false;
            return DateTime.TryParseExact(dateString.Trim(), HttpDateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsedUtc);
        }

        public static HttpsTimeResult QueryHttpDate(string url, int timeoutMs = 4000)
        {
            EnforceModernTls();

            HttpsTimeResult result = new HttpsTimeResult
            {
                Url = url,
                Success = false
            };

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                using (HttpClientHandler handler = new HttpClientHandler { UseProxy = true })
                using (HttpClient client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; WinTime)");

                    using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Head, url))
                    {
                        using (HttpResponseMessage res = client.SendAsync(req).GetAwaiter().GetResult())
                        {
                            sw.Stop();
                            result.LatencyMs = (int)sw.ElapsedMilliseconds;

                            string rawDateHeader = null;
                            if (res.Headers.Date.HasValue)
                            {
                                DateTimeOffset dto = res.Headers.Date.Value;
                                result.UtcTime = dto.UtcDateTime.AddMilliseconds(result.LatencyMs / 2.0);
                                result.Success = true;
                                return result;
                            }

                            IEnumerable<string> headerValues;
                            if (res.Headers.TryGetValues("Date", out headerValues))
                            {
                                foreach (string val in headerValues)
                                {
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        rawDateHeader = val;
                                        break;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(rawDateHeader))
                            {
                                result.ErrorMessage = "HTTP response is missing 'Date' header.";
                                return result;
                            }

                            DateTime parsedUtc;
                            if (TryParseHttpDate(rawDateHeader, out parsedUtc))
                            {
                                result.UtcTime = parsedUtc.AddMilliseconds(result.LatencyMs / 2.0);
                                result.Success = true;
                                return result;
                            }

                            result.ErrorMessage = "Failed to parse HTTP Date header format: " + rawDateHeader;
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.LatencyMs = (int)sw.ElapsedMilliseconds;

                string fullError = ex.ToString();
                string message = ex.Message;

                bool isTimeout = ex is TaskCanceledException ||
                                 ex is TimeoutException ||
                                 (ex.InnerException is TimeoutException) ||
                                 (ex.InnerException is WebException && ((WebException)ex.InnerException).Status == WebExceptionStatus.Timeout);

                bool isTlsError = fullError.IndexOf("AuthenticationException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  fullError.IndexOf("TrustFailure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  fullError.IndexOf("SecureChannelFailure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  message.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  message.IndexOf("secure channel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  fullError.IndexOf("RemoteCertificateValidationCallback", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isTlsError)
                {
                    result.IsTlsClockSkewError = true;
                    result.ErrorMessage = "TLS certificate validation failed. Your local system clock may be severely skewed (e.g. game preset) causing HTTPS certificates to appear invalid. Use standard SNTP (UDP 123) to resync.";
                }
                else if (isTimeout)
                {
                    result.ErrorMessage = string.Format("HTTPS request to '{0}' timed out after {1} ms.", url, timeoutMs);
                }
                else
                {
                    result.ErrorMessage = "HTTP request failed: " + ex.Message;
                }
                return result;
            }
        }
    }
}
