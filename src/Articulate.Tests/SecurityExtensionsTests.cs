#nullable enable
using NUnit.Framework;

namespace Articulate.Tests
{
    [TestFixture]
    public class SecurityExtensionsTests
    {
        [Test]
        public void ToSafeCssUrl_preserves_absolute_uri_delimiters_and_query_string()
        {
            string? result = "https://example.com/media/foo bar.jpg?width=100&height=50&hmac=abc".ToSafeCssUrl();

            Assert.That(result, Is.EqualTo("https://example.com/media/foo%20bar.jpg?width=100&height=50&hmac=abc"));
        }

        [Test]
        public void ToSafeCssUrl_encodes_relative_url_spaces()
        {
            string? result = "/media/foo bar.jpg?width=100&height=50&hmac=abc".ToSafeCssUrl();

            Assert.That(result, Is.EqualTo("/media/foo%20bar.jpg?width=100&height=50&hmac=abc"));
        }

        [TestCase("javascript:alert(1)")]
        [TestCase("data:image/svg+xml,<svg></svg>")]
        [TestCase("//example.com/image.jpg")]
        public void ToSafeCssUrl_rejects_unsafe_css_urls(string url)
        {
            string? result = url.ToSafeCssUrl();

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ToSafeCssUrl_escapes_css_context_characters()
        {
            string? result = "/media/foo(1)'wide'.jpg".ToSafeCssUrl();

            Assert.That(result, Is.EqualTo("/media/foo\\(1\\)\\'wide\\'.jpg"));
        }

        [Test]
        public void ToCssStyleAttributeValue_returns_background_image_fragment_for_safe_url()
        {
            string result = "/media/foo bar.jpg?width=100&height=50&hmac=abc".ToCssStyleAttributeValue();

            Assert.That(result, Is.EqualTo("background-image: url('/media/foo%20bar.jpg?width=100&height=50&hmac=abc');"));
        }

        [Test]
        public void ToCssStyleAttributeValue_returns_empty_string_for_unsafe_url()
        {
            string result = "javascript:alert(1)".ToCssStyleAttributeValue();

            Assert.That(result, Is.EqualTo(string.Empty));
        }
    }
}
