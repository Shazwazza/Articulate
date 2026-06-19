#nullable enable
using Articulate.Routing;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRouteChangeDetectorTests
    {
        [Test]
        public void AffectsArticulateRoutes_returns_false_for_whitespace_changed_path()
        {
            IPublishedContent articulateNode = CreatePublishedContent("-1,100,200", 2, 0);

            var result = ArticulateRouteChangeDetector.AffectsArticulateRoutes("   ", 1, 0, "Home", [articulateNode]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void AffectsArticulateRoutes_returns_false_when_no_articulate_nodes_exist()
        {
            var result = ArticulateRouteChangeDetector.AffectsArticulateRoutes("-1,100", 1, 0, "Home", []);

            Assert.That(result, Is.False);
        }

        [Test]
        public void AffectsArticulateRoutes_returns_false_when_changed_path_matches_same_node_only()
        {
            IPublishedContent articulateNode = CreatePublishedContent("-1,100,200", 2, 0);

            var result =
                ArticulateRouteChangeDetector.AffectsArticulateRoutes("-1,100,200", 2, 5, "Home", [articulateNode]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void AffectsArticulateRoutes_returns_false_when_same_level_sort_order_does_not_move_ahead_of_root()
        {
            IPublishedContent articulateNode = CreatePublishedContent("-1,200", 1, 5);

            var result =
                ArticulateRouteChangeDetector.AffectsArticulateRoutes("-1,150", 1, 5, "Sibling", [articulateNode]);

            Assert.That(result, Is.False);
        }

        private static IPublishedContent CreatePublishedContent(string path, int level, int sortOrder)
        {
            Mock<IPublishedContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.Articulate);

            Mock<IPublishedContent> content = new();
            content.SetupGet(x => x.Path).Returns(path);
            content.SetupGet(x => x.Level).Returns(level);
            content.SetupGet(x => x.SortOrder).Returns(sortOrder);
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            return content.Object;
        }
    }
}
