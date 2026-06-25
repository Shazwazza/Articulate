#nullable enable
using Articulate.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Articulate.Tests.Components
{
    [TestFixture]
    public class ContentPublishingHandlerTests
    {
        [Test]
        public void Handle_cancels_publish_when_configured_route_segments_collide()
        {
            IContent root = CreateContent(ArticulateConstants.ContentType.Articulate);
            IContent firstChild = CreateContent("child");
            IContent secondChild = CreateContent("child");
            ContentPublishingNotification notification = new(root, new EventMessages());

            CreateSut([firstChild, secondChild], "shared").Handle(notification);

            Assert.That(notification.Cancel, Is.True);
            Assert.That(notification.Messages, Is.Not.Empty);
        }

        [Test]
        public void Handle_ignores_non_articulate_content()
        {
            ContentPublishingNotification notification = new(CreateContent("textPage"), new EventMessages());

            CreateSut().Handle(notification);

            Assert.That(notification.Cancel, Is.False);
        }

        private static ContentPublishingHandler CreateSut(
            IReadOnlyList<IContent>? children = null,
            string? routeSegment = null)
        {
            children ??= [];
            Mock<IContentService> contentService = new();
            contentService
                .Setup(x => x.GetPagedChildren(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    out It.Ref<long>.IsAny,
                    null,
                    null,
                    null,
                    true))
                .Returns((
                    int _,
                    long _,
                    int _,
                    out long total,
                    string[]? _,
                    IQuery<IContent>? _,
                    Ordering? _,
                    bool _) =>
                {
                    total = children.Count;
                    return children;
                });
            Mock<IUrlSegmentProvider> urlSegmentProvider = new();
            urlSegmentProvider
                .Setup(x => x.GetUrlSegment(It.IsAny<IContentBase>(), false, null))
                .Returns(routeSegment);

            return new ContentPublishingHandler(
                contentService.Object,
                Mock.Of<IDomainCacheService>(),
                Mock.Of<IShortStringHelper>(),
                [urlSegmentProvider.Object],
                Mock.Of<ISqlContext>(),
                NullLogger<ContentPublishingHandler>.Instance);
        }

        private static IContent CreateContent(string alias)
        {
            Mock<ISimpleContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(alias);

            Mock<IContent> content = new();
            content.SetupGet(x => x.Id).Returns(100);
            content.SetupGet(x => x.Name).Returns("Blog");
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            return content.Object;
        }
    }
}
