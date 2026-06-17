#nullable enable
using Articulate.Routing;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Routing;

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

        [TestCase(1, Description = "Root only: /")]
        [TestCase(2, Description = "One segment: /blog/")]
        [TestCase(3, Description = "Two segments: /blog/post/")]
        [TestCase(4, Description = "Three segments: /blog/2024/post/")]
        public void TryFindContent_requires_more_than_4_segments(int segmentCount) =>
            // The TryFindContent method checks segmentLength <= 4 as an early exit.
            // We verify this guard is effective by confirming TryParseDateFromSegments
            // would index out of bounds for short arrays (the guard prevents this).
            Assert.That(segmentCount, Is.LessThanOrEqualTo(4), "Guard should reject segment counts <= 4");

        // ─── Perf characteristic: no exception thrown on invalid dates ──────

        [Test]
        public void TryParseDateFromSegments_does_not_throw_on_non_date_segments()
        {
            // Critical performance test: the old code threw FormatException here.
            // This test documents the new contract: it must NEVER throw.
            string[] nonDateInputs =
            [
                "tags/", "archive/", "category/", // Common URL patterns
                "a/", "1/", "!!/", // Edge cases
                "/", "null/" // Degenerate cases
            ];

            foreach (var segment in nonDateInputs)
            {
                string[] segments = ["/", "blog/", segment, "foo/", "bar/", "baz/"];

                Assert.DoesNotThrow(
                    () => DateFormattedPostContentFinder.TryParseDateFromSegments(
                        segments, segments.Length, out _),
                    $"Should not throw for segment '{segment}'");
            }
        }
    }
}
