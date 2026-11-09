#nullable enable
using Articulate.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class DateFormattedPostContentFinderTests
    {
        // ─── TryParseDateFromSegments ───────────────────────────────────────

        [TestCase(
            "2024/", "06/", "13/", 2024, 6, 13, Description = "Standard date")]
        [TestCase("2020/", "02/", "29/", 2020, 2, 29, Description = "Leap year Feb 29")]
        [TestCase("1999/", "12/", "31/", 1999, 12, 31, Description = "End of century")]
        [TestCase("2025/", "01/", "01/", 2025, 1, 1, Description = "New Year's Day")]
        public void TryParseDateFromSegments_valid_dates_return_true(
            string yearSeg,
            string monthSeg,
            string daySeg,
            int expectedYear,
            int expectedMonth,
            int expectedDay)
        {
            // URI.Segments for /blog/2024/06/13/my-post/ are:
            //   ["/", "blog/", "2024/", "06/", "13/", "my-post/"]
            //   segmentLength=6, date segments are at indices 2,3,4 (i.e. length-4, length-3, length-2)
            string[] segments = ["/", "blog/", yearSeg, monthSeg, daySeg, "my-post/"];

            var result = DateFormattedPostContentFinder.TryParseDateFromSegments(
                segments, segments.Length, out DateTime postDate);

            Assert.That(result, Is.True);
            Assert.That(postDate.Year, Is.EqualTo(expectedYear));
            Assert.That(postDate.Month, Is.EqualTo(expectedMonth));
            Assert.That(postDate.Day, Is.EqualTo(expectedDay));
        }

        [TestCase("blog/", "archive/", "tags/", Description = "Alphabetic segments (most common miss)")]
        [TestCase("abc/", "de/", "fg/", Description = "Short alpha segments")]
        [TestCase("20xx/", "06/", "13/", Description = "Non-numeric year")]
        [TestCase("2024/", "13/", "01/", Description = "Month 13 is invalid")]
        [TestCase("2024/", "02/", "30/", Description = "Feb 30 is invalid")]
        [TestCase("2023/", "02/", "29/", Description = "Feb 29 on non-leap year")]
        [TestCase("24/", "06/", "13/", Description = "Two-digit year")]
        [TestCase("2024/", "6/", "3/", Description = "Single-digit month/day without leading zero")]
        public void TryParseDateFromSegments_invalid_dates_return_false(
            string seg1,
            string seg2,
            string seg3)
        {
            string[] segments = ["/", "blog/", seg1, seg2, seg3, "my-post/"];

            var result = DateFormattedPostContentFinder.TryParseDateFromSegments(
                segments, segments.Length, out DateTime postDate);

            Assert.That(result, Is.False);
            Assert.That(postDate, Is.EqualTo(default(DateTime)));
        }

        [Test]
        public void TryParseDateFromSegments_trailing_slash_is_trimmed_from_day_segment()
        {
            // The day segment from Uri.Segments always has a trailing slash.
            // Verify TrimEnd('/') works correctly.
            string[] segments = ["/", "blog/", "2024/", "06/", "13/", "post/"];

            var result = DateFormattedPostContentFinder.TryParseDateFromSegments(
                segments, segments.Length, out DateTime postDate);

            Assert.That(result, Is.True);
            Assert.That(postDate, Is.EqualTo(new DateTime(2024, 6, 13)));
        }

        [Test]
        public void TryParseDateFromSegments_day_without_trailing_slash_still_works()
        {
            // Edge case: last segment without trailing slash (unlikely but defensive).
            string[] segments = ["/", "blog/", "2024/", "06/", "13", "post/"];

            var result = DateFormattedPostContentFinder.TryParseDateFromSegments(
                segments, segments.Length, out DateTime postDate);

            Assert.That(result, Is.True);
            Assert.That(postDate, Is.EqualTo(new DateTime(2024, 6, 13)));
        }

        // ─── BuildRouteWithoutDateSegments ──────────────────────────────────

        [Test]
        public void BuildRouteWithoutDateSegments_strips_date_segments_and_lowercases()
        {
            // Segments: ["/", "Blog/", "2024/", "06/", "13/", "My-Post/"]
            // Expected: strips indices 2,3,4 → "/blog/my-post/"
            var uri = new Uri("https://example.com/Blog/2024/06/13/My-Post/");
            Mock<IPublishedRequestBuilder> requestBuilder = new();
            requestBuilder.SetupGet(x => x.Uri).Returns(uri);
            requestBuilder.SetupGet(x => x.Domain).Returns((DomainAndUri?)null);

            var result = DateFormattedPostContentFinder.BuildRouteWithoutDateSegments(
                requestBuilder.Object, uri.Segments.Length);

            Assert.That(result, Is.EqualTo("/blog/my-post/"));
        }

        [Test]
        public void BuildRouteWithoutDateSegments_preserves_deeper_paths()
        {
            // /en/blog/2024/06/13/some-post/
            // Segments: ["/", "en/", "blog/", "2024/", "06/", "13/", "some-post/"]
            // Strips indices 3,4,5 → "/en/blog/some-post/"
            var uri = new Uri("https://example.com/en/blog/2024/06/13/some-post/");
            Mock<IPublishedRequestBuilder> requestBuilder = new();
            requestBuilder.SetupGet(x => x.Uri).Returns(uri);
            requestBuilder.SetupGet(x => x.Domain).Returns((DomainAndUri?)null);

            var result = DateFormattedPostContentFinder.BuildRouteWithoutDateSegments(
                requestBuilder.Object, uri.Segments.Length);

            Assert.That(result, Is.EqualTo("/en/blog/some-post/"));
        }

        // ─── Integration: segment count guard ───────────────────────────────

        [TestCase("https://example.com/", Description = "Root only: /")]
        [TestCase("https://example.com/blog/", Description = "One segment: /blog/")]
        [TestCase("https://example.com/blog/post/", Description = "Two segments: /blog/post/")]
        [TestCase("https://example.com/blog/2024/post/", Description = "Three segments: /blog/2024/post/")]
        public async Task TryFindContent_requires_more_than_4_segments(string requestUri)
        {
            var finder = new DateFormattedPostContentFinder(
#if UMBRACO_18_OR_GREATER
                Mock.Of<ILogger<ContentFinderByUrl>>(),
#else
                Mock.Of<ILogger<ContentFinderByUrlNew>>(),
#endif
                Mock.Of<IUmbracoContextAccessor>(),
                Mock.Of<IDocumentUrlService>(),
                Mock.Of<IPublishedContentCache>(),
                Mock.Of<IOptionsMonitor<WebRoutingSettings>>(),
                Mock.Of<IDocumentNavigationQueryService>(),
                Mock.Of<IPublishedContentStatusFilteringService>(),
                Mock.Of<IPublishedValueFallback>());
            Mock<IPublishedRequestBuilder> requestBuilder = new();
            requestBuilder.SetupGet(x => x.Uri).Returns(new Uri(requestUri));

            var result = await finder.TryFindContent(requestBuilder.Object);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task TryFindContent_sets_published_content_for_matching_dated_route()
        {
            var postDate = new DateTime(2024, 6, 13);
            var postKey = Guid.NewGuid();
            var parentKey = Guid.NewGuid();
            var rootKey = Guid.NewGuid();

            Mock<IPublishedProperty> publishedDateProperty = new();
            publishedDateProperty.Setup(x => x.HasValue(null, null)).Returns(true);
            publishedDateProperty.Setup(x => x.GetValue(null, null)).Returns(postDate);

            Mock<IPublishedProperty> dateFormatProperty = new();
            dateFormatProperty.Setup(x => x.HasValue(null, null)).Returns(true);
            dateFormatProperty.Setup(x => x.GetValue(null, null)).Returns(true);

            Mock<IPublishedContentType> postContentType = new();
            postContentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.ArticulateMarkdown);
            postContentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);

            Mock<IPublishedContentType> parentContentType = new();
            parentContentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);

            Mock<IPublishedContentType> rootContentType = new();
            rootContentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);

            Mock<IPublishedContent> post = new();
            post.SetupGet(x => x.Key).Returns(postKey);
            post.SetupGet(x => x.ContentType).Returns(postContentType.Object);
            post.Setup(x => x.GetProperty("publishedDate")).Returns(publishedDateProperty.Object);

            Mock<IPublishedContent> parent = new();
            parent.SetupGet(x => x.Key).Returns(parentKey);
            parent.SetupGet(x => x.ContentType).Returns(parentContentType.Object);

            Mock<IPublishedContent> root = new();
            root.SetupGet(x => x.Key).Returns(rootKey);
            root.SetupGet(x => x.ContentType).Returns(rootContentType.Object);
            root.Setup(x => x.GetProperty("useDateFormatForUrl")).Returns(dateFormatProperty.Object);

            Mock<IDocumentNavigationQueryService> navigationQueryService = new();
            Guid? postParentKey = parentKey;
            Guid? parentParentKey = rootKey;
            navigationQueryService
                .Setup(x => x.TryGetParentKey(postKey, out postParentKey))
                .Returns(true);
            navigationQueryService
                .Setup(x => x.TryGetParentKey(parentKey, out parentParentKey))
                .Returns(true);

            Mock<IPublishedContentStatusFilteringService> statusFilteringService = new();
#pragma warning disable CS0618
            statusFilteringService
                .Setup(x => x.Unfiltered(It.Is<IEnumerable<Guid>>(keys => keys.SequenceEqual(new[] { parentKey }))))
                .Returns([parent.Object]);
            statusFilteringService
                .Setup(x => x.Unfiltered(It.Is<IEnumerable<Guid>>(keys => keys.SequenceEqual(new[] { rootKey }))))
                .Returns([root.Object]);
#pragma warning restore CS0618

            Mock<IUmbracoContext> umbracoContext = new();
            umbracoContext.SetupGet(x => x.InPreviewMode).Returns(false);
            Mock<IUmbracoContextAccessor> umbracoContextAccessor = new();
            IUmbracoContext context = umbracoContext.Object;
#pragma warning disable CS8600
            umbracoContextAccessor
                .Setup(x => x.TryGetUmbracoContext(out context))
                .Returns(true);
#pragma warning restore CS8600

            Mock<IDocumentUrlService> documentUrlService = new();
            documentUrlService
                .Setup(x => x.GetDocumentKeyByRoute("/blog/my-post/", null, null, false))
                .Returns(postKey);

            Mock<IPublishedContentCache> publishedContentCache = new();
            publishedContentCache
                .Setup(x => x.GetById(false, postKey))
                .Returns(post.Object);

            Mock<IPublishedValueFallback> publishedValueFallback = new();
            DateFormattedPostContentFinder finder = new(
#if UMBRACO_18_OR_GREATER
                    Mock.Of<ILogger<ContentFinderByUrl>>(),
#else
                    Mock.Of<ILogger<ContentFinderByUrlNew>>(),
#endif
                umbracoContextAccessor.Object,
                documentUrlService.Object,
                publishedContentCache.Object,
                Mock.Of<IOptionsMonitor<WebRoutingSettings>>(),
                navigationQueryService.Object,
                statusFilteringService.Object,
                publishedValueFallback.Object);
            Mock<IPublishedRequestBuilder> requestBuilder = new();
                requestBuilder.SetupGet(x => x.Uri).Returns(
                    new Uri("https://example.com/blog/2024/06/13/my-post/"));
                IPublishedContent? selectedContent = null;
                requestBuilder
                    .Setup(x => x.SetPublishedContent(It.IsAny<IPublishedContent>()))
                    .Callback<IPublishedContent>(content => selectedContent = content)
                    .Returns(requestBuilder.Object);

                var result = await finder.TryFindContent(requestBuilder.Object);

            Assert.That(result, Is.True);
            Assert.That(selectedContent, Is.SameAs(post.Object));
        }

        // ─── Perf characteristic: no exception thrown on invalid dates ──────

        [Test]
        public void TryParseDateFromSegments_does_not_throw_on_non_date_segments()
        {
            // Critical performance test: the old code threw FormatException here.
            // This test documents the new contract: it must NEVER throw.
            var nonDateInputs = new[]
            {
                "tags/", "archive/", "category/", // Common URL patterns
                "a/", "1/", "!!/", // Edge cases
                "/", "null/" // Degenerate cases
            };

            foreach (var segment in nonDateInputs)
            {
                var segments = new[] { "/", "blog/", segment, "foo/", "bar/", "baz/" };

                Assert.DoesNotThrow(
                    () => DateFormattedPostContentFinder.TryParseDateFromSegments(
                        segments, segments.Length, out _),
                    $"Should not throw for segment '{segment}'");
            }
        }
    }
}
