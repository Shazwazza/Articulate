#nullable enable
using Articulate.Routing;
using NUnit.Framework;
using Umbraco.Cms.Core.Routing;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateDomainMatcherTests
    {
        [Test]
        public void Matches_returns_false_for_culture_mismatch()
        {
            Domain candidate = new(10, "blog.local", 1, "en-US", false, 0);
            Domain currentDomain = new(11, "blog.local", 2, "da-DK", false, 0);

            var matches = ArticulateDomainMatcher.Matches(candidate, currentDomain);

            Assert.That(matches, Is.False);
        }

        [Test]
        public void Matches_returns_false_for_wildcard_mismatch()
        {
            Domain candidate = new(10, "blog.local", 1, string.Empty, true, 0);
            Domain currentDomain = new(11, "blog.local", 2, string.Empty, false, 0);

            var matches = ArticulateDomainMatcher.Matches(candidate, currentDomain);

            Assert.That(matches, Is.False);
        }

        [Test]
        public void Matches_returns_false_for_same_host_with_different_paths()
        {
            Domain candidate = new(10, "blog.local/articles/", 1, string.Empty, false, 0);
            Domain currentDomain = new(11, "https://blog.local/news/", 2, string.Empty, false, 0);

            var matches = ArticulateDomainMatcher.Matches(candidate, currentDomain, new Uri("https://blog.local/"));

            Assert.That(matches, Is.False);
        }

        [Test]
        public void Matches_returns_true_for_equivalent_path_based_domains()
        {
            Domain candidate = new(10, "blog.local/articles/", 1, string.Empty, false, 0);
            Domain currentDomain = new(11, "https://blog.local/articles", 2, string.Empty, false, 0);

            var matches = ArticulateDomainMatcher.Matches(candidate, currentDomain, new Uri("https://blog.local/"));

            Assert.That(matches, Is.True);
        }

        [Test]
        public void Matches_falls_back_to_normalized_name_when_uri_cannot_be_parsed()
        {
            Domain candidate = new(10, "bad host/", 1, string.Empty, false, 0);
            Domain currentDomain = new(11, "bad host", 2, string.Empty, false, 0);

            var matches = ArticulateDomainMatcher.Matches(candidate, currentDomain, new Uri("https://example.local/"));

            Assert.That(matches, Is.True);
        }
    }
}
