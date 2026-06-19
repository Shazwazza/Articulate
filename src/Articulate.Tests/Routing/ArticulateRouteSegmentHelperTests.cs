#nullable enable
using Articulate.Routing;
using NUnit.Framework;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRouteSegmentHelperTests
    {
        [TestCase(" tags ", "tags")]
        [TestCase("/tags/", "tags")]
        [TestCase(" /search/ ", "search")]
        public void NormalizeOrNull_trims_wrapping_whitespace_and_slashes(string routeSegment, string expected)
        {
            var normalizedRouteSegment = ArticulateRouteSegmentHelper.NormalizeOrNull(routeSegment);

            Assert.That(normalizedRouteSegment, Is.EqualTo(expected));
        }

        [Test]
        public void CombineRoutePath_returns_normalized_route_path()
        {
            var routePath = ArticulateRouteSegmentHelper.CombineRoutePath("/blog/", " /topics/ ");

            Assert.That(routePath, Is.EqualTo("/blog/topics"));
        }

        [Test]
        public void CombineRoutePath_returns_null_for_empty_value()
        {
            var routePath = ArticulateRouteSegmentHelper.CombineRoutePath("/blog/", "///");

            Assert.That(routePath, Is.Null);
        }
    }
}
