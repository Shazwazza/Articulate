#nullable enable
using Articulate.Routing;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
#if UMBRACO_18_OR_GREATER
using Umbraco.Cms.Core.Services;
#endif

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRouteValidatorTests
    {
        [Test]
        public void DomainsForContent_returns_domains_for_root_and_ancestors_in_path()
        {
            IPublishedContent content = CreateRoot(id: 200, path: "-1,100,200");
            List<Domain> domains =
            [
                new(10, "section.local", 100, string.Empty, false, 0),
                new(11, "blog.local", 200, string.Empty, false, 0),
                new(12, "elsewhere.local", 999, string.Empty, false, 0)
            ];

            List<Domain> matchedDomains = ArticulateRouteValidator.DomainsForContent(content, domains);

            Assert.That(matchedDomains.Select(x => x.ContentId), Is.EqualTo(new[] { 100, 200 }));
        }

        [Test]
        public void DomainsForContent_returns_empty_when_no_domains_match_path()
        {
            IPublishedContent content = CreateRoot(id: 200, path: "-1,100,200");
            List<Domain> domains =
            [
                new(10, "elsewhere.local", 999, string.Empty, false, 0)
            ];

            List<Domain> matchedDomains = ArticulateRouteValidator.DomainsForContent(content, domains);

            Assert.That(matchedDomains, Is.Empty);
        }

        [Test]
        public void DomainsForContent_throws_invalid_operation_for_malformed_path()
        {
            IPublishedContent root = CreateRoot(path: "-1,not-an-int");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                ArticulateRouteValidator.DomainsForContent(root, []))!;

            Assert.That(ex.Message, Does.Contain("invalid path"));
        }

        [Test]
        public void ValidateRootPathMappings_rejects_same_path_without_domains()
        {
            IPublishedContent left = CreateRoot(id: 1, name: "Blog A", path: "-1,1");
            IPublishedContent right = CreateRoot(id: 2, name: "Blog B", path: "-1,2");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                ArticulateRouteValidator.ValidateRootPathMappings(
                    "/blog/",
                    [left, right],
                    [],
                    new Uri("https://example.local/")))!;

            Assert.That(ex.Message, Does.Contain("Ambiguous Articulate root routing"));
        }

        [Test]
        public void ValidateRootPathMappings_allows_same_path_with_distinct_domains()
        {
            IPublishedContent left = CreateRoot(id: 1, name: "Blog A", path: "-1,1");
            IPublishedContent right = CreateRoot(id: 2, name: "Blog B", path: "-1,2");

            List<Domain> domains =
            [
                new(10, "a.local", 1, string.Empty, false, 0),
                new(11, "b.local", 2, string.Empty, false, 0)
            ];

            Assert.DoesNotThrow(() =>
                ArticulateRouteValidator.ValidateRootPathMappings(
                    "/blog/",
                    [left, right],
                    domains,
                    new Uri("https://example.local/")));
        }

        [Test]
        public void ValidateRootPathMappings_rejects_same_path_with_equivalent_domains_even_when_ids_differ()
        {
            IPublishedContent left = CreateRoot(id: 1, name: "Blog A", path: "-1,1");
            IPublishedContent right = CreateRoot(id: 2, name: "Blog B", path: "-1,2");

            List<Domain> domains =
            [
                new(10, "blog.local", 1, string.Empty, false, 0),
                new(11, "https://blog.local/", 2, string.Empty, false, 0)
            ];

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                ArticulateRouteValidator.ValidateRootPathMappings(
                    "/blog/",
                    [left, right],
                    domains,
                    new Uri("https://blog.local/")))!;

            Assert.That(ex.Message, Does.Contain("Ambiguous Articulate root routing"));
        }

        [Test]
        public void ValidateRootPathMappings_allows_single_root_without_domains()
        {
            IPublishedContent single = CreateRoot(id: 1, name: "Blog", path: "-1,1");

            Assert.DoesNotThrow(() =>
                ArticulateRouteValidator.ValidateRootPathMappings(
                    "/blog/",
                    [single],
                    [],
                    new Uri("https://example.local/")));
        }

        [Test]
        public void ValidateRootPathMappings_allows_same_path_with_same_host_but_distinct_cultures()
        {
            IPublishedContent left = CreateRoot(id: 1, name: "Blog A", path: "-1,1");
            IPublishedContent right = CreateRoot(id: 2, name: "Blog B", path: "-1,2");

            List<Domain> domains =
            [
                new(10, "blog.local", 1, "en-US", false, 0),
                new(11, "blog.local", 2, "da-DK", false, 0)
            ];

            Assert.DoesNotThrow(() =>
                ArticulateRouteValidator.ValidateRootPathMappings(
                    "/blog/",
                    [left, right],
                    domains,
                    new Uri("https://blog.local/")));
        }

        [Test]
        public void ValidateRootPathMappings_rejects_same_path_with_equivalent_path_based_domains()
        {
            IPublishedContent left = CreateRoot(id: 1, name: "Blog A", path: "-1,1");
            IPublishedContent right = CreateRoot(id: 2, name: "Blog B", path: "-1,2");

            List<Domain> domains =
            [
                new(10, "blog.local/articles/", 1, string.Empty, false, 0),
                new(11, "https://blog.local/articles", 2, string.Empty, false, 0)
            ];

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                ArticulateRouteValidator.ValidateRootPathMappings(
                    "/blog/",
                    [left, right],
                    domains,
                    new Uri("https://blog.local/")))!;

            Assert.That(ex.Message, Does.Contain("Ambiguous Articulate root routing"));
        }

        [Test]
        public void ValidateConfiguredRouteSegments_rejects_duplicate_child_route_segments()
        {
            IPublishedContent root = CreateRoot();

#if UMBRACO_18_OR_GREATER
            IPublishedContent[] children = [
                CreateRoot(name: "Child A", urlSegment: "tags"),
                CreateRoot(name: "Child B", urlSegment: "Tags")
            ];
            Mock<IDocumentUrlService> documentUrlService = new();
            documentUrlService.Setup(x => x.GetUrlSegment(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>()))
#pragma warning disable CS0618 // Type or member is obsolete
                .Returns((Guid key, string culture, bool ignoreOverride) => children.First(c => c.Key == key).UrlSegment);
#pragma warning restore CS0618 // Type or member is obsolete
#endif

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                ArticulateRouteValidator.ValidateConfiguredRouteSegments(
                    root,
#if UMBRACO_18_OR_GREATER
                    documentUrlService.Object,
                    children))!;
#else
                    [
                        CreateRoot(name: "Child A", urlSegment: "tags"),
                        CreateRoot(name: "Child B", urlSegment: "Tags")
                    ]))!;
#endif

            Assert.That(ex.Message, Does.Contain("child content 'Child A'"));
            Assert.That(ex.Message, Does.Contain("child content 'Child B'"));
        }

        [TestCase("   ", "non-slash character")]
        [TestCase("///", "non-slash character")]
        [TestCase(" / / ", "single URL segment")]
        public void ValidateConfiguredRouteSegment_rejects_invalid_route_segments(
            string routeSegment,
            string expectedMessageFragment)
        {
            IPublishedContent content = CreateRoot();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                ArticulateRouteValidator.ValidateConfiguredRouteSegment(
                    content,
                    "searchUrlName",
                    routeSegment,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)))!;

            Assert.That(ex.Message, Does.Contain(expectedMessageFragment));
        }

        [Test]
        public void ValidateConfiguredRouteSegment_allows_empty_route_segment()
        {
            IPublishedContent content = CreateRoot();

            Assert.DoesNotThrow(() =>
                ArticulateRouteValidator.ValidateConfiguredRouteSegment(
                    content,
                    "searchUrlName",
                    string.Empty,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
        }

        private static IPublishedContent CreateRoot(
            int id = 1,
            string name = "Blog",
            string path = "-1,1",
            string urlSegment = "blog")
        {
            Mock<IPublishedContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.Articulate);

            Mock<IPublishedContent> root = new();
            root.SetupGet(x => x.Id).Returns(id);
            root.SetupGet(x => x.Name).Returns(name);
            root.SetupGet(x => x.Path).Returns(path);
            root.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            root.SetupGet(x => x.Properties).Returns([]);
            root.Setup(x => x.GetProperty(It.IsAny<string>())).Returns((IPublishedProperty?)null);
            root.SetupGet(x => x.SortOrder).Returns(0);
            root.SetupGet(x => x.CreatorId).Returns(0);
            root.SetupGet(x => x.CreateDate).Returns(DateTime.UtcNow);
            root.SetupGet(x => x.WriterId).Returns(0);
            root.SetupGet(x => x.UpdateDate).Returns(DateTime.UtcNow);
            root.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>());
            root.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);
            root.Setup(x => x.IsDraft(It.IsAny<string?>())).Returns(false);
            root.Setup(x => x.IsPublished(It.IsAny<string?>())).Returns(true);
            root.SetupGet(x => x.Level).Returns(1);
            root.SetupGet(x => x.TemplateId).Returns((int?)null);
#pragma warning disable CS0618 // Type or member is obsolete
            root.SetupGet(x => x.UrlSegment).Returns(urlSegment);
#pragma warning restore CS0618 // Type or member is obsolete
            root.SetupGet(x => x.ContentType).Returns(contentType.Object);
            return root.Object;
        }
    }
}
