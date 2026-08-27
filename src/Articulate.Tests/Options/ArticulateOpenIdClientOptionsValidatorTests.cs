using Articulate.Options;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using OpenIddict.Abstractions;

namespace Articulate.Tests.Options
{
    [TestFixture]
    public class ArticulateOpenIdClientOptionsValidatorTests
    {
        private readonly ArticulateOpenIdClientOptionsValidator _sut = new();

        [Test]
        public void Validate_accepts_public_client_without_secret()
        {
            ValidateOptionsResult result = _sut.Validate(null, CreateOptions());

            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Validate_rejects_confidential_client()
        {
            ArticulateOpenIdClientOptions options = CreateOptions();
            options.ClientType = OpenIddictConstants.ClientTypes.Confidential;
            options.ClientSecret = "secret";

            ValidateOptionsResult result = _sut.Validate(null, options);

            Assert.That(result.Failed, Is.True);
            Assert.That(result.FailureMessage, Does.Contain("ClientType must be Public"));
        }

        [Test]
        public void Validate_rejects_secret_for_public_client()
        {
            ArticulateOpenIdClientOptions options = CreateOptions();
            options.ClientSecret = "secret";

            ValidateOptionsResult result = _sut.Validate(null, options);

            Assert.That(result.Failed, Is.True);
            Assert.That(result.FailureMessage, Does.Contain("ClientSecret is not supported"));
        }

        private static ArticulateOpenIdClientOptions CreateOptions() => new()
        {
            Enabled = true,
            ClientId = "umbraco-articulate",
            RedirectUris = ["https://localhost/a-new/"]
        };
    }
}
