#nullable enable
using Articulate.MetaWeblog;
using NUnit.Framework;

namespace Articulate.Tests.MetaWeblog
{
    [TestFixture]
    public class ArticulateMetaWeblogProviderTests
    {
        [Test]
        public void StripInvalidImageUrls_removes_img_with_file_protocol()
        {
            const string input = """<p>before <img src="file:///etc/passwd"> after</p>""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Does.Not.Contain("<img"));
        }

        [Test]
        public void StripInvalidImageUrls_removes_img_with_javascript_protocol()
        {
            const string input = """<p><img src="javascript:alert(1)"></p>""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Does.Not.Contain("<img"));
        }

        [Test]
        public void StripInvalidImageUrls_removes_img_with_data_uri()
        {
            const string input = """<img src="data:image/png;base64,abc123">""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Does.Not.Contain("<img"));
        }

        [Test]
        public void StripInvalidImageUrls_removes_entire_paragraph_wrapping_invalid_img()
        {
            const string input = """<p><img src="file:///etc/passwd"></p>""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Does.Not.Contain("<p>"));
            Assert.That(result, Does.Not.Contain("<img"));
        }

        [Test]
        public void StripInvalidImageUrls_preserves_img_with_https_url()
        {
            const string input = """<img src="https://cdn.example.com/image.png">""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void StripInvalidImageUrls_preserves_img_with_http_url()
        {
            const string input = """<img src="http://cdn.example.com/image.png">""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void StripInvalidImageUrls_preserves_img_with_media_path()
        {
            const string input = """<img src="/media/12345/image.png">""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Is.EqualTo(input));
        }

        [Test]
        public void StripInvalidImageUrls_removes_invalid_and_preserves_valid_in_mixed_content()
        {
            const string input =
                """<img src="file:///bad.png"> text <img src="https://good.com/img.png">""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Does.Contain("https://good.com/img.png"));
            Assert.That(result, Does.Not.Contain("file:///bad.png"));
        }

        [Test]
        public void StripInvalidImageUrls_removes_img_with_protocol_relative_url()
        {
            const string input = """<img src="//evil.com/track.png">""";
            var result = ArticulateMetaWeblogProvider.StripInvalidImageUrls(input);
            Assert.That(result, Does.Not.Contain("<img"));
        }
    }
}
