#nullable enable
using System.Net;
using System.Reflection;
using Articulate.Services;
using NUnit.Framework;

namespace Articulate.Tests.Services
{
    [TestFixture]
    public class ArticulateImportMediaRedirectTests
    {
        [Test]
        public void Redirect_rejects_https_to_http_downgrade()
        {
            using HttpResponseMessage response = Redirect(HttpStatusCode.Redirect, "http://example.com/image.png");

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                GetValidatedRedirectUri(response, new Uri("https://example.com/start"), 0))!;

            Assert.That(exception.InnerException, Is.TypeOf<HttpRequestException>());
            Assert.That(exception.InnerException!.Message, Does.Contain("cannot downgrade"));
        }

        [Test]
        public void Redirect_resolves_relative_location_against_current_uri()
        {
            using HttpResponseMessage response = Redirect(HttpStatusCode.Redirect, "../image.png");

            Uri result = GetValidatedRedirectUri(response, new Uri("https://example.com/path/start"), 0);

            Assert.That(result, Is.EqualTo(new Uri("https://example.com/image.png")));
        }

        [Test]
        public void Redirect_without_location_fails_closed()
        {
            using HttpResponseMessage response = new(HttpStatusCode.Redirect);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                GetValidatedRedirectUri(response, new Uri("https://example.com/start"), 0))!;

            Assert.That(exception.InnerException, Is.TypeOf<HttpRequestException>());
        }

        [Test]
        public void Redirect_limit_fails_closed()
        {
            using HttpResponseMessage response = Redirect(HttpStatusCode.Redirect, "/image.png");

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                GetValidatedRedirectUri(response, new Uri("https://example.com/start"), 5))!;

            Assert.That(exception.InnerException, Is.TypeOf<HttpRequestException>());
            Assert.That(exception.InnerException!.Message, Does.Contain("Too many redirects"));
        }

        private static HttpResponseMessage Redirect(HttpStatusCode statusCode, string location)
        {
            HttpResponseMessage response = new(statusCode);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        }

        private static Uri GetValidatedRedirectUri(HttpResponseMessage response, Uri currentUri, int redirectCount) =>
            (Uri)typeof(ArticulateImportMediaService)
                .GetMethod("GetValidatedRedirectUri", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [response, currentUri, redirectCount])!;
    }
}
