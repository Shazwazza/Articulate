using Articulate.Controllers;
using NUnit.Framework;

namespace Articulate.Tests.Controllers
{
    [TestFixture]
    public class MarkdownEditorBaseUrlResolverTests
    {
        private const string CloudUrl = "https://example.euwest01.umbraco.io/";

        [Test]
        public void ResolveBaseUrl_uses_request_origin_when_it_matches_a_redirect_uri()
        {
            string result = MarkdownEditorBaseUrlResolver.ResolveBaseUrl(
                "https://example.com/",
                CloudUrl,
                [CloudUrl + "a-new/", "https://example.com/a-new/"],
                out MarkdownEditorBaseUrlSource source);

            Assert.That(result, Is.EqualTo("https://example.com/"));
            Assert.That(source, Is.EqualTo(MarkdownEditorBaseUrlSource.TrustedRequestOrigin));
        }

        [Test]
        public void ResolveBaseUrl_matches_origin_case_insensitively_and_keeps_path_base()
        {
            string result = MarkdownEditorBaseUrlResolver.ResolveBaseUrl(
                "https://EXAMPLE.com/blog",
                CloudUrl,
                ["https://example.com/blog/a-new/"],
                out MarkdownEditorBaseUrlSource source);

            Assert.That(result, Is.EqualTo("https://example.com/blog"));
            Assert.That(source, Is.EqualTo(MarkdownEditorBaseUrlSource.TrustedRequestOrigin));
        }

        [TestCase("https://evil.example/")]
        [TestCase("http://example.com/")]
        [TestCase("https://example.com:8443/")]
        [TestCase("https://user@example.com/")]
        [TestCase("not a url")]
        [TestCase(null)]
        public void ResolveBaseUrl_falls_back_to_application_url_for_untrusted_request_origin(string requestUrl)
        {
            string result = MarkdownEditorBaseUrlResolver.ResolveBaseUrl(
                requestUrl,
                CloudUrl,
                ["https://example.com/a-new/"],
                out MarkdownEditorBaseUrlSource source);

            Assert.That(result, Is.EqualTo(CloudUrl));
            Assert.That(source, Is.EqualTo(MarkdownEditorBaseUrlSource.UmbracoApplicationUrl));
        }

        [Test]
        public void ResolveBaseUrl_ignores_invalid_and_non_http_redirect_uris()
        {
            string result = MarkdownEditorBaseUrlResolver.ResolveBaseUrl(
                "https://example.com/",
                CloudUrl,
                ["/a-new/", "ftp://example.com/a-new/", string.Empty],
                out MarkdownEditorBaseUrlSource source);

            Assert.That(result, Is.EqualTo(CloudUrl));
            Assert.That(source, Is.EqualTo(MarkdownEditorBaseUrlSource.UmbracoApplicationUrl));
        }

        [Test]
        public void ResolveBaseUrl_uses_request_when_no_application_url_is_configured()
        {
            string result = MarkdownEditorBaseUrlResolver.ResolveBaseUrl(
                "https://localhost:44300/",
                null,
                [],
                out MarkdownEditorBaseUrlSource source);

            Assert.That(result, Is.EqualTo("https://localhost:44300/"));
            Assert.That(source, Is.EqualTo(MarkdownEditorBaseUrlSource.RequestDerived));
        }

        [Test]
        public void GetPostLogoutRedirectUrl_prefers_same_origin_uri()
        {
            string result = MarkdownEditorBaseUrlResolver.GetPostLogoutRedirectUrl(
                new Uri("https://example.com/"),
                [CloudUrl + "a-new/", "https://example.com/a-new/"]);

            Assert.That(result, Is.EqualTo("https://example.com/a-new/"));
        }

        [Test]
        public void GetPostLogoutRedirectUrl_falls_back_to_first_valid_uri()
        {
            string result = MarkdownEditorBaseUrlResolver.GetPostLogoutRedirectUrl(
                new Uri("https://example.com/"),
                ["/relative/", CloudUrl + "a-new/"]);

            Assert.That(result, Is.EqualTo(CloudUrl + "a-new/"));
        }

        [Test]
        public void GetPostLogoutRedirectUrl_falls_back_to_base_root()
        {
            string result = MarkdownEditorBaseUrlResolver.GetPostLogoutRedirectUrl(
                new Uri("https://example.com/blog/a-new/?x=1"),
                []);

            Assert.That(result, Is.EqualTo("https://example.com/"));
        }
    }
}
