#nullable enable
using System.Net;
using Articulate.Services;
using NUnit.Framework;

namespace Articulate.Tests.Services
{
    [TestFixture]
    public class ExternalImageHostPolicyTests
    {
        [Test]
        public void ValidateHost_returns_null_for_allowlisted_public_host()
        {
            var result = ExternalImageHostPolicy.ValidateHost(
                "Images.Example.com.",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "images.example.com" },
                false,
                false);

            Assert.That(result, Is.Null);
        }

        [TestCase("metadata.amazonaws.com")]
        [TestCase("anything.localtest.me")]
        [TestCase("cdn.nip.io")]
        public void ValidateHost_blocks_metadata_and_rebinding_hosts_even_when_allowlisted(string host)
        {
            var result = ExternalImageHostPolicy.ValidateHost(
                host,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { host },
                true,
                false);

            Assert.That(result, Does.Contain("is not allowed"));
        }

        [Test]
        public void ValidateHost_blocks_localhost_name_unless_development_override_is_enabled()
        {
            var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost" };

            var productionResult = ExternalImageHostPolicy.ValidateHost(
                "localhost",
                allowedHosts,
                false,
                true);
            var developmentResult = ExternalImageHostPolicy.ValidateHost(
                "localhost",
                allowedHosts,
                true,
                false);

            Assert.That(productionResult, Does.Contain("is a local host"));
            Assert.That(developmentResult, Is.Null);
        }

        [TestCase("169.254.169.254")]
        [TestCase("168.63.129.16")]
        public void ValidateHost_blocks_metadata_literal_addresses_even_when_development_override_is_enabled(
            string host)
        {
            var result = ExternalImageHostPolicy.ValidateHost(
                host,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { host },
                true,
                false);

            Assert.That(result, Does.Contain("is not allowed"));
        }

        [Test]
        public void ValidateResolvedAddresses_blocks_private_addresses_without_development_override()
        {
            var result = ExternalImageHostPolicy.ValidateResolvedAddresses(
                [IPAddress.Parse("10.0.0.5")],
                "images.example.com",
                false);

            Assert.That(result, Does.Contain("is not allowed"));
        }

        [Test]
        public void ValidateResolvedAddresses_keeps_metadata_addresses_blocked_with_development_override()
        {
            var result = ExternalImageHostPolicy.ValidateResolvedAddresses(
                [IPAddress.Parse("169.254.169.254")],
                "metadata.example.com",
                true);

            Assert.That(result, Does.Contain("is not allowed"));
        }
    }
}
