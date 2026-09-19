using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace WindowsTimeManager.Core
{
    public class PeerValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public string CleanHost { get; set; }
        public string Flag { get; set; }
        public string FullEntry { get; set; }

        public override string ToString()
        {
            return IsValid ? FullEntry : string.Format("Invalid ({0})", ErrorMessage);
        }
    }

    /// <summary>
    /// Strict semantic validator for NTP peer addresses (IPv4, IPv6, and FQDNs).
    /// Rejects loopback, multicast, broadcast, invalid labels, and reserved ranges.
    /// </summary>
    public static class PeerValidator
    {
        public static PeerValidationResult Validate(string rawInput, string defaultFlag = "0x8")
        {
            PeerValidationResult result = new PeerValidationResult
            {
                IsValid = false,
                Flag = defaultFlag
            };

            if (string.IsNullOrEmpty(rawInput))
            {
                result.ErrorMessage = "Peer entry cannot be empty.";
                return result;
            }

            string input = rawInput.Trim();
            string hostPart = input;
            string flagPart = defaultFlag;

            if (input.Contains(","))
            {
                string[] split = input.Split(new char[] { ',' }, 2);
                hostPart = split[0].Trim();
                if (split.Length > 1 && !string.IsNullOrEmpty(split[1].Trim()))
                {
                    flagPart = split[1].Trim();
                }
            }

            if (string.IsNullOrEmpty(hostPart))
            {
                result.ErrorMessage = "Hostname or IP address is empty.";
                return result;
            }

            // Flag syntax check: must be hexadecimal (e.g. 0x8, 0x1, 0x2, 0x9) or numeric
            if (!Regex.IsMatch(flagPart, @"^(0x[0-9a-fA-F]+|[0-9]+)$"))
            {
                result.ErrorMessage = string.Format("Invalid NTP flag format: '{0}'. Expected hexadecimal (e.g. 0x8).", flagPart);
                return result;
            }

            // Check for invalid control or non-ASCII characters
            foreach (char c in hostPart)
            {
                if (c > 127 || char.IsControl(c) || char.IsWhiteSpace(c) ||
                    c == '/' || c == '\\' || c == '?' || c == '*' || c == '!' || c == ';' || c == '"' || c == '\'')
                {
                    result.ErrorMessage = string.Format("Hostname contains illegal characters: '{0}'. Only English letters, numbers, hyphens, and dots are permitted.", c);
                    return result;
                }
            }

            // Reject explicit 'localhost'
            if (hostPart.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = "Localhost loopback address cannot be used as an external NTP peer.";
                return result;
            }

            // Case 1: IP Address validation
            IPAddress ip;
            if (IPAddress.TryParse(hostPart, out ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] bytes = ip.GetAddressBytes();

                    // Loopback check (127.0.0.0/8)
                    if (bytes[0] == 127)
                    {
                        result.ErrorMessage = "Loopback address (127.0.0.0/8) cannot be used as a system NTP peer.";
                        return result;
                    }

                    // Any/Unspecified check (0.0.0.0)
                    if (bytes[0] == 0)
                    {
                        result.ErrorMessage = "Unspecified address (0.0.0.0) cannot be used as an NTP peer.";
                        return result;
                    }

                    // Broadcast check (255.255.255.255)
                    if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255)
                    {
                        result.ErrorMessage = "Broadcast address (255.255.255.255) cannot be used as an NTP peer.";
                        return result;
                    }

                    // Multicast check (224.0.0.0 to 239.255.255.255)
                    if (bytes[0] >= 224 && bytes[0] <= 239)
                    {
                        result.ErrorMessage = "Multicast address range (224.0.0.0/4) cannot be configured as a manual unicast peer.";
                        return result;
                    }
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // IPv6 Loopback (::1)
                    if (IPAddress.IsLoopback(ip))
                    {
                        result.ErrorMessage = "IPv6 Loopback address (::1) cannot be used as an external NTP peer.";
                        return result;
                    }

                    // IPv6 Unspecified (::)
                    if (ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any))
                    {
                        result.ErrorMessage = "IPv6 Unspecified address (::) cannot be used as an NTP peer.";
                        return result;
                    }

                    // IPv6 Multicast (ff00::/8)
                    if (ip.IsIPv6Multicast)
                    {
                        result.ErrorMessage = "IPv6 Multicast addresses cannot be configured as a manual unicast peer.";
                        return result;
                    }
                }

                result.IsValid = true;
                result.CleanHost = hostPart;
                result.Flag = flagPart;
                result.FullEntry = string.Format("{0},{1}", hostPart, flagPart);
                return result;
            }

            // Case 2: Fully Qualified Domain Name (FQDN) validation
            if (hostPart.Length < 3 || hostPart.Length > 253)
            {
                result.ErrorMessage = string.Format("Hostname length ({0}) is out of bounds (must be 3 to 253 characters).", hostPart.Length);
                return result;
            }

            if (!hostPart.Contains("."))
            {
                result.ErrorMessage = "Public NTP hostname must be a fully qualified domain name (FQDN) containing at least one domain dot (e.g. pool.ntp.org).";
                return result;
            }

            string[] labels = hostPart.Split('.');
            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                if (string.IsNullOrEmpty(label))
                {
                    result.ErrorMessage = "Hostname contains empty domain labels (consecutive or trailing dots).";
                    return result;
                }

                if (label.Length > 63)
                {
                    result.ErrorMessage = string.Format("Domain label '{0}' exceeds maximum allowed length of 63 characters.", label);
                    return result;
                }

                if (label.StartsWith("-") || label.EndsWith("-"))
                {
                    result.ErrorMessage = string.Format("Domain label '{0}' cannot start or end with a hyphen.", label);
                    return result;
                }

                if (!Regex.IsMatch(label, @"^[a-zA-Z0-9-]+$"))
                {
                    result.ErrorMessage = string.Format("Domain label '{0}' contains illegal characters.", label);
                    return result;
                }
            }

            // TLD check (last label cannot be all-numeric for FQDNs)
            string tld = labels[labels.Length - 1];
            if (Regex.IsMatch(tld, @"^[0-9]+$"))
            {
                result.ErrorMessage = "Top-level domain (TLD) cannot be purely numeric.";
                return result;
            }

            result.IsValid = true;
            result.CleanHost = hostPart.ToLowerInvariant();
            result.Flag = flagPart;
            result.FullEntry = string.Format("{0},{1}", result.CleanHost, flagPart);
            return result;
        }
    }
}
