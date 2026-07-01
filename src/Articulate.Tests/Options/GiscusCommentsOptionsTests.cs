using Articulate.Options;
using NUnit.Framework;

namespace Articulate.Tests.Options
{
    [TestFixture]
    public class GiscusCommentsOptionsTests
    {
        [Test]
        public void DataLoading_AcceptsLazyValue()
        {
            var options = new GiscusCommentsOptions { DataLoading = "lazy" };

            Assert.That(options.DataLoading, Is.EqualTo("lazy"));
        }

        [Test]
        public void ArticulateCommentsOptions_DefaultsGiscusToNewInstance()
        {
            var options = new ArticulateCommentsOptions();

            Assert.That(options.Giscus, Is.Not.Null);
            Assert.That(options.Giscus, Is.InstanceOf<GiscusCommentsOptions>());
        }

        [Test]
        public void AllowedCorsOrigins_DefaultsToGiscusApp()
        {
            var options = new GiscusCommentsOptions();

            Assert.That(options.AllowedCorsOrigins, Is.EqualTo(new[] { "https://giscus.app" }));
        }

        [Test]
        public void ResolveCorsHeaders_NoOrigin_ReturnsWildcardWithoutVary()
        {
            CorsHeaderDecision decision = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: null,
                allowedOrigins: new[] { "https://giscus.app" });

            Assert.That(decision.AllowOrigin, Is.EqualTo("*"));
            Assert.That(decision.Vary, Is.False);
        }

        [Test]
        public void ResolveCorsHeaders_EmptyOrigin_ReturnsWildcardWithoutVary()
        {
            CorsHeaderDecision decision = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: string.Empty,
                allowedOrigins: new[] { "https://giscus.app" });

            Assert.That(decision.AllowOrigin, Is.EqualTo("*"));
            Assert.That(decision.Vary, Is.False);
        }

        [Test]
        public void ResolveCorsHeaders_MatchingOrigin_ReflectsAndSetsVary()
        {
            CorsHeaderDecision decision = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: "https://giscus.app",
                allowedOrigins: new[] { "https://giscus.app", "https://comments.example.com" });

            Assert.That(decision.AllowOrigin, Is.EqualTo("https://giscus.app"));
            Assert.That(decision.Vary, Is.True);
        }

        [Test]
        public void ResolveCorsHeaders_MatchingOrigin_IsCaseInsensitive()
        {
            CorsHeaderDecision decision = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: "HTTPS://Giscus.APP",
                allowedOrigins: new[] { "https://giscus.app" });

            Assert.That(decision.AllowOrigin, Is.EqualTo("HTTPS://Giscus.APP"));
            Assert.That(decision.Vary, Is.True);
        }

        [Test]
        public void ResolveCorsHeaders_UntrustedOrigin_ReturnsNoHeaders()
        {
            CorsHeaderDecision decision = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: "https://evil.example.com",
                allowedOrigins: new[] { "https://giscus.app" });

            Assert.That(decision.AllowOrigin, Is.Null);
            Assert.That(decision.Vary, Is.False);
        }

        [Test]
        public void ResolveCorsHeaders_NullAllowedOrigins_TreatsAsEmptyForCrossOrigin()
        {
            // No-Origin request → * (same-origin / server-to-server are unaffected).
            CorsHeaderDecision sameOrigin = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: null,
                allowedOrigins: null);
            Assert.That(sameOrigin.AllowOrigin, Is.EqualTo("*"));
            Assert.That(sameOrigin.Vary, Is.False);

            // Origin present but no allowlist → no CORS headers (browser blocks).
            CorsHeaderDecision crossOrigin = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: "https://giscus.app",
                allowedOrigins: null);
            Assert.That(crossOrigin.AllowOrigin, Is.Null);
            Assert.That(crossOrigin.Vary, Is.False);
        }

        [Test]
        public void ResolveCorsHeaders_AllowsSelfHostedOrigin()
        {
            CorsHeaderDecision decision = GiscusCommentsOptions.ResolveCorsHeaders(
                requestOrigin: "https://comments.example.com",
                allowedOrigins: new[] { "https://giscus.app", "https://comments.example.com" });

            Assert.That(decision.AllowOrigin, Is.EqualTo("https://comments.example.com"));
            Assert.That(decision.Vary, Is.True);
        }
    }
}