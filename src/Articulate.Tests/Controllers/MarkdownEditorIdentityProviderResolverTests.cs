#nullable enable
using Articulate.Controllers;
using NUnit.Framework;

namespace Articulate.Tests.Controllers
{
    [TestFixture]
    public class MarkdownEditorIdentityProviderResolverTests
    {
        private const string UmbracoId = "Umbraco.UmbracoId";
        private const string Google = "Umbraco.Google";

        [Test]
        public void Resolve_uses_configured_registered_provider()
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                " " + Google + " ",
                [UmbracoId, Google],
                denyLocalLogin: false,
                out bool notFound);

            Assert.That(result, Is.EqualTo(Google));
            Assert.That(notFound, Is.False);
        }

        [Test]
        public void Resolve_configured_local_provider_forces_local_login()
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                "Umbraco",
                [UmbracoId],
                denyLocalLogin: true,
                out bool notFound);

            Assert.That(result, Is.Null);
            Assert.That(notFound, Is.False);
        }

        [TestCase("Umbraco.Missing")]
        [TestCase("umbraco.umbracoid")]
        public void Resolve_flags_unregistered_configured_provider(string configured)
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                configured,
                [UmbracoId],
                denyLocalLogin: false,
                out bool notFound);

            Assert.That(result, Is.Null);
            Assert.That(notFound, Is.True);
        }

        [Test]
        public void Resolve_auto_selects_umbraco_id_when_it_is_the_only_provider()
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                null,
                [UmbracoId],
                denyLocalLogin: false,
                out bool notFound);

            Assert.That(result, Is.EqualTo(UmbracoId));
            Assert.That(notFound, Is.False);
        }

        [Test]
        public void Resolve_auto_selects_only_provider_when_local_login_is_denied()
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                string.Empty,
                [Google],
                denyLocalLogin: true,
                out _);

            Assert.That(result, Is.EqualTo(Google));
        }

        [Test]
        public void Resolve_keeps_local_login_when_local_login_is_allowed()
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                null,
                [Google],
                denyLocalLogin: false,
                out _);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Resolve_keeps_local_login_when_several_providers_are_registered()
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                null,
                [UmbracoId, Google],
                denyLocalLogin: true,
                out _);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Resolve_keeps_local_login_when_no_providers_are_registered()
        {
            string? result = MarkdownEditorIdentityProviderResolver.Resolve(
                null,
                [],
                denyLocalLogin: false,
                out _);

            Assert.That(result, Is.Null);
        }
    }
}
