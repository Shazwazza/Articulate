#nullable enable
using Articulate;
using NUnit.Framework;
using Provider = Articulate.ArticulateConstants.Comments.Provider;

namespace Articulate.Tests.Models
{
    [TestFixture]
    public class ArticulateCommentsProviderResolutionTests
    {
        [Test]
        public void ResolveProvider_ReturnsNone_WhenNoProviderIsConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: false, giscusConfigured: false);

            Assert.That(result, Is.EqualTo(Provider.None));
        }

        [Test]
        public void ResolveProvider_ReturnsGiscus_WhenOnlyGiscusIsConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: false, giscusConfigured: true);

            Assert.That(result, Is.EqualTo(Provider.Giscus));
        }

        [Test]
        public void ResolveProvider_ReturnsDisqus_WhenOnlyDisqusIsConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: true, giscusConfigured: false);

            Assert.That(result, Is.EqualTo(Provider.Disqus));
        }

        [Test]
        public void ResolveProvider_ReturnsDisqus_WhenBothProvidersAreConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: true, giscusConfigured: true);

            Assert.That(result, Is.EqualTo(Provider.Disqus));
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
