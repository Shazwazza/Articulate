#nullable enable
using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;

namespace Articulate.Services
{
    internal static class ExternalImageHostPolicy
    {
        private static readonly FrozenSet<string> _alwaysBlockedHostNames =
            FrozenSet.ToFrozenSet([
                "metadata.amazonaws.com",
                "metadata.google.internal"
            ]);

        private static readonly FrozenSet<string> _alwaysBlockedHostSuffixes =
            FrozenSet.ToFrozenSet([
                "localtest.me",
                "lvh.me",
                "nip.io",
                "sslip.io",
                "traefik.me",
                "xip.io"
            ]);

        private static readonly IPAddress _azurePlatformAddress = IPAddress.Parse("168.63.129.16");
        private static readonly IPAddress _awsIpv6MetadataAddress = IPAddress.Parse("fd00:ec2::254");

        public static string? ValidateHost(
            string host,
            ISet<string> allowedHosts,
            bool allowUnsafeLocalExternalImageHosts,
            bool isProductionMode)
        {
            var normalizedHost = NormalizeHost(host);
            if (normalizedHost.Length == 0)
            {
                return "Image URL host cannot be empty";
            }

            if (IsAlwaysBlockedHost(normalizedHost))
            {
                return $"Host '{host}' is not allowed for external image downloads";
            }

            if (allowedHosts.Count == 0)
            {
                return "External image downloads are disabled because no allowed media hosts are configured";
            }

            if (allowedHosts.All(x => NormalizeHost(x) != normalizedHost))
            {
                return $"Host '{host}' is not configured in Articulate:AllowedMediaHosts";
            }

            if (IPAddress.TryParse(normalizedHost, out IPAddress? literalAddress) &&
                IsDisallowedAddress(literalAddress, allowUnsafeLocalExternalImageHosts))
            {
                return $"Address '{literalAddress}' for host '{host}' is not allowed";
            }

            if (IsLocalhostName(normalizedHost) && !allowUnsafeLocalExternalImageHosts)
            {
                return isProductionMode
                    ? $"Host '{host}' is a local host and is not allowed in production"
                    : $"Host '{host}' is a local host and is blocked because AllowUnsafeLocalExternalImageHostsInDevelopment is disabled";
            }

            return null;
        }

        public static string? ValidateResolvedAddresses(
            IEnumerable<IPAddress> addresses,
            string host,
            bool allowUnsafeLocalExternalImageHosts)
        {
            foreach (IPAddress address in addresses)
            {
                if (IsDisallowedAddress(address, allowUnsafeLocalExternalImageHosts))
                {
                    return $"Resolved address '{address}' for host '{host}' is not allowed";
                }
            }

            return null;
        }

        private static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();

        private static bool IsAlwaysBlockedHost(string normalizedHost) =>
            _alwaysBlockedHostNames.Contains(normalizedHost) ||
            _alwaysBlockedHostSuffixes.Any(suffix =>
                normalizedHost == suffix || normalizedHost.EndsWith($".{suffix}", StringComparison.Ordinal));

        private static bool IsLocalhostName(string normalizedHost) =>
            normalizedHost == "localhost" ||
            normalizedHost.EndsWith(".localhost", StringComparison.Ordinal);

        private static bool IsDisallowedAddress(IPAddress address, bool allowUnsafeLocalExternalImageHosts)
        {
            if (address.Equals(IPAddress.Any) ||
                address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.None) ||
                address.Equals(IPAddress.IPv6None))
            {
                return true;
            }

            if (!allowUnsafeLocalExternalImageHosts && IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return IsDisallowedIPv6Address(address, allowUnsafeLocalExternalImageHosts);
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                return true;
            }

            return IsDisallowedIPv4Address(address, allowUnsafeLocalExternalImageHosts);
        }

        private static bool IsDisallowedIPv6Address(IPAddress address, bool allowUnsafeLocalExternalImageHosts)
        {
            if (address.IsIPv6Multicast)
            {
                return true;
            }

            if (address.Equals(IPAddress.IPv6Loopback))
            {
                return !allowUnsafeLocalExternalImageHosts;
            }

            if (TryGetEmbeddedIPv4Address(address, out IPAddress embeddedIPv4Address))
            {
                return IsDisallowedIPv4Address(embeddedIPv4Address, allowUnsafeLocalExternalImageHosts);
            }

            if (address.Equals(_awsIpv6MetadataAddress))
            {
                return true;
            }

            var bytes = address.GetAddressBytes();
            if (allowUnsafeLocalExternalImageHosts)
            {
                return false;
            }

            return address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   bytes[0] == 0 ||
                   (bytes[0] & 0xFE) == 0xFC;
        }

        private static bool IsDisallowedIPv4Address(IPAddress address, bool allowUnsafeLocalExternalImageHosts)
        {
            var ipv4 = address.GetAddressBytes();

            if (IsAlwaysBlockedIPv4Address(ipv4) || IsUnroutableIPv4Address(ipv4))
            {
                return true;
            }

            if (allowUnsafeLocalExternalImageHosts)
            {
                return false;
            }

            return
                ipv4[0] == 10 ||
                ipv4[0] == 127 ||
                (ipv4[0] == 100 && ipv4[1] >= 64 && ipv4[1] <= 127) ||
                (ipv4[0] == 169 && ipv4[1] == 254) ||
                (ipv4[0] == 172 && ipv4[1] >= 16 && ipv4[1] <= 31) ||
                (ipv4[0] == 192 && ipv4[1] == 168) ||
                (ipv4[0] == 198 && (ipv4[1] == 18 || ipv4[1] == 19));
        }

        private static bool IsUnroutableIPv4Address(byte[] ipv4) =>
            ipv4[0] == 0 ||
            ipv4[0] >= 224;

        private static bool TryGetEmbeddedIPv4Address(IPAddress address, out IPAddress embeddedIPv4Address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                embeddedIPv4Address = address.MapToIPv4();
                return true;
            }

            var bytes = address.GetAddressBytes();
            if (IsIPv4CompatibleIPv6Address(bytes) ||
                IsIPv4TranslatedIPv6Address(bytes) ||
                IsWellKnownNat64Address(bytes))
            {
                embeddedIPv4Address = new IPAddress(bytes[^4..]);
                return true;
            }

            embeddedIPv4Address = IPAddress.None;
            return false;
        }

        private static bool IsIPv4CompatibleIPv6Address(byte[] bytes) =>
            bytes.Take(12).All(x => x == 0) &&
            bytes.Skip(12).Any(x => x != 0);

        private static bool IsIPv4TranslatedIPv6Address(byte[] bytes) =>
            bytes.Take(8).All(x => x == 0) &&
            bytes[8] == 0xFF &&
            bytes[9] == 0xFF &&
            bytes[10] == 0 &&
            bytes[11] == 0;

        private static bool IsWellKnownNat64Address(byte[] bytes) =>
            bytes[0] == 0x00 &&
            bytes[1] == 0x64 &&
            bytes[2] == 0xFF &&
            bytes[3] == 0x9B &&
            bytes.Skip(4).Take(8).All(x => x == 0);

        private static bool IsAlwaysBlockedIPv4Address(byte[] ipv4) =>
            (ipv4[0] == 169 && ipv4[1] == 254 && ipv4[2] == 169 && ipv4[3] == 254) ||
            (ipv4[0] == 169 && ipv4[1] == 254 && ipv4[2] == 170 && ipv4[3] == 2) ||
            (ipv4[0] == 100 && ipv4[1] == 100 && ipv4[2] == 100 && ipv4[3] == 200) ||
            ipv4.SequenceEqual(_azurePlatformAddress.GetAddressBytes());
    }
}
