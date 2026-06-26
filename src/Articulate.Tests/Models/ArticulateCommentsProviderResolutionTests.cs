#nullable enable
using NUnit.Framework;

namespace Articulate.Tests.Models
{
    [TestFixture]
    public class ArticulateCommentsProviderResolutionTests
    {
        [Test]
        public void ResolveProvider_ReturnsNone_WhenNoProviderIsConfigured()
        {
            var result = MasterModel.ResolveProvider(disqusShortNameSet: false, giscusConfigured: false);

            Assert.That(result, Is.EqualTo("None"));
        }

        [Test]
        public void ResolveProvider_ReturnsGiscus_WhenOnlyGiscusIsConfigured()
        {
            var result = MasterModel.ResolveProvider(disqusShortNameSet: false, giscusConfigured: true);

            Assert.That(result, Is.EqualTo("Giscus"));
        }

        [Test]
        public void ResolveProvider_ReturnsDisqus_WhenOnlyDisqusIsConfigured()
        {
            var result = MasterModel.ResolveProvider(disqusShortNameSet: true, giscusConfigured: false);

            Assert.That(result, Is.EqualTo("Disqus"));
        }

        [Test]
        public void ResolveProvider_ReturnsDisqus_WhenBothProvidersAreConfigured()
        {
            var result = MasterModel.ResolveProvider(disqusShortNameSet: true, giscusConfigured: true);

            Assert.That(result, Is.EqualTo("Disqus"));
        }

        [TestCase(null, "fallback", "fallback")]
        [TestCase("", "fallback", "fallback")]
        [TestCase("   ", "fallback", "fallback")]
        [TestCase("override", "fallback", "override")]
        public void ResolveGiscusValue_PrefersDocTypeValue_AndFallsBackToAppsettings(
            string? docTypeValue, string appsettingsFallback, string expected)
        {
            var result = MasterModel.ResolveGiscusValue(docTypeValue, appsettingsFallback);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
