#nullable enable
using Articulate.Components;
using Articulate.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Sync;

namespace Articulate.Tests.Components
{
    [TestFixture]
    public class ContentCacheRefresherHandlerTests
    {
        [Test]
        public void AffectsArticulateRoutes_returns_true_for_articulate_root_change()
        {
            bool result = ArticulateRouteChangeDetector.AffectsArticulateRoutes(
                "-1,100",
                1,
                0,
                ArticulateConstants.ContentType.Articulate,
                []);

            Assert.That(result, Is.True);
        }

        [Test]
        public void AffectsArticulateRoutes_returns_true_for_ancestor_of_nested_root()
        {
            IPublishedContent nestedRoot = CreatePublishedContent(
                id: 200,
                path: "-1,100,200",
                level: 2,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            bool result = ArticulateRouteChangeDetector.AffectsArticulateRoutes("-1,100", 1, 0, "Home", [nestedRoot]);

            Assert.That(result, Is.True);
        }

        [Test]
        public void AffectsArticulateRoutes_returns_false_for_unrelated_content()
        {
            IPublishedContent nestedRoot = CreatePublishedContent(
                id: 200,
                path: "-1,100,200",
                level: 2,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            bool result = ArticulateRouteChangeDetector.AffectsArticulateRoutes("-1,999", 1, 0, "Elsewhere", [nestedRoot]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void AffectsArticulateRoutes_returns_true_for_same_level_sort_precedence_change()
        {
            IPublishedContent root = CreatePublishedContent(
                id: 200,
                path: "-1,200",
                level: 1,
                sortOrder: 5,
                alias: ArticulateConstants.ContentType.Articulate);

            bool result = ArticulateRouteChangeDetector.AffectsArticulateRoutes("-1,150", 1, 1, "Sibling", [root]);

            Assert.That(result, Is.True);
        }

        [Test]
        public void Handle_refreshes_routes_for_refresh_by_id_when_content_affects_routes()
        {
            IPublishedContent changedContent = CreatePublishedContent(
                id: 100,
                path: "-1,100",
                level: 1,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            ContentCacheRefresherHandler sut = CreateSut(
                [],
                out Mock<IArticulateRouteRefreshState> routeRefreshState,
                CreateUmbracoContextAccessor(liveContent: changedContent));

            sut.Handle(new ContentCacheRefresherNotification(100, MessageType.RefreshById));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Once);
        }

        [Test]
        public void Handle_does_not_refresh_routes_for_refresh_by_id_when_content_does_not_affect_routes()
        {
            IPublishedContent articulateRoot = CreatePublishedContent(
                id: 200,
                path: "-1,100,200",
                level: 2,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            IPublishedContent changedContent = CreatePublishedContent(
                id: 999,
                path: "-1,999",
                level: 1,
                sortOrder: 0,
                alias: "Elsewhere");

            ContentCacheRefresherHandler sut = CreateSut(
                [articulateRoot],
                out Mock<IArticulateRouteRefreshState> routeRefreshState,
                CreateUmbracoContextAccessor(liveContent: changedContent));

            sut.Handle(new ContentCacheRefresherNotification(999, MessageType.RefreshById));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Never);
        }

        [Test]
        public void Handle_refresh_by_payload_refreshes_when_payload_contains_refresh_node()
        {
            IPublishedContent changedContent = CreatePublishedContent(
                id: 100,
                path: "-1,100",
                level: 1,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            ContentCacheRefresherHandler sut = CreateSut(
                [],
                out Mock<IArticulateRouteRefreshState> routeRefreshState,
                CreateUmbracoContextAccessor(liveContent: changedContent));

            sut.Handle(new ContentCacheRefresherNotification(
                new[] { new ContentCacheRefresher.JsonPayload { Id = 100, ChangeTypes = TreeChangeTypes.RefreshNode } },
                MessageType.RefreshByPayload));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Once);
        }

        [Test]
        public void Handle_refresh_by_payload_ignores_payload_without_relevant_change_types()
        {
            ContentCacheRefresherHandler sut = CreateSut([], out Mock<IArticulateRouteRefreshState> routeRefreshState);

            sut.Handle(new ContentCacheRefresherNotification(
                new[] { new ContentCacheRefresher.JsonPayload { Id = 100, ChangeTypes = TreeChangeTypes.None } },
                MessageType.RefreshByPayload));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Never);
        }

        [Test]
        public void Handle_remove_by_id_refreshes_routes_when_content_cannot_be_resolved()
        {
            Mock<IPublishedContentCache> publishedContentCache = new();
            publishedContentCache.Setup(x => x.GetById(100)).Returns((IPublishedContent?)null);
            publishedContentCache.Setup(x => x.GetById(true, 100)).Returns((IPublishedContent?)null);

            ContentCacheRefresherHandler sut = CreateSut(
                [],
                out Mock<IArticulateRouteRefreshState> routeRefreshState,
                CreateUmbracoContextAccessor(publishedContentCache: publishedContentCache.Object));

            sut.Handle(new ContentCacheRefresherNotification(100, MessageType.RemoveById));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Once);
        }

        [Test]
        public void Handle_refresh_by_id_returns_when_umbraco_context_is_unavailable()
        {
            Mock<Umbraco.Cms.Core.Web.IUmbracoContextAccessor> umbracoContextAccessor = new();
            Umbraco.Cms.Core.Web.IUmbracoContext? context = null;
            umbracoContextAccessor
                .Setup(x => x.TryGetUmbracoContext(out context))
                .Returns(false);

            ContentCacheRefresherHandler sut = CreateSut(
                [],
                out Mock<IArticulateRouteRefreshState> routeRefreshState,
                umbracoContextAccessor.Object);

            sut.Handle(new ContentCacheRefresherNotification(100, MessageType.RefreshById));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Never);
        }

        [Test]
        public void Handle_refresh_by_id_does_not_refresh_when_only_preview_content_exists()
        {
            IPublishedContent changedContent = CreatePublishedContent(
                id: 100,
                path: "-1,100",
                level: 1,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            Mock<IPublishedContentCache> publishedContentCache = new();
            publishedContentCache.Setup(x => x.GetById(100)).Returns((IPublishedContent?)null);
            publishedContentCache.Setup(x => x.GetById(true, 100)).Returns(changedContent);

            ContentCacheRefresherHandler sut = CreateSut(
                [],
                out Mock<IArticulateRouteRefreshState> routeRefreshState,
                CreateUmbracoContextAccessor(publishedContentCache: publishedContentCache.Object));

            sut.Handle(new ContentCacheRefresherNotification(100, MessageType.RefreshById));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Never);
            publishedContentCache.Verify(x => x.GetById(true, 100), Times.Once);
        }

        [Test]
        public void Handle_remove_by_id_uses_preview_lookup_when_live_lookup_returns_null()
        {
            IPublishedContent changedContent = CreatePublishedContent(
                id: 100,
                path: "-1,100",
                level: 1,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            Mock<IPublishedContentCache> publishedContentCache = new();
            publishedContentCache.Setup(x => x.GetById(100)).Returns((IPublishedContent?)null);
            publishedContentCache.Setup(x => x.GetById(true, 100)).Returns(changedContent);

            ContentCacheRefresherHandler sut = CreateSut(
                [],
                out Mock<IArticulateRouteRefreshState> routeRefreshState,
                CreateUmbracoContextAccessor(publishedContentCache: publishedContentCache.Object));

            sut.Handle(new ContentCacheRefresherNotification(100, MessageType.RemoveById));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Once);
            publishedContentCache.Verify(x => x.GetById(true, 100), Times.Once);
        }

        [Test]
        public void Handle_refresh_by_instance_ignores_non_content_message_object()
        {
            ContentCacheRefresherHandler sut = CreateSut([], out Mock<IArticulateRouteRefreshState> routeRefreshState);

            sut.Handle(new ContentCacheRefresherNotification("not-content", MessageType.RefreshByInstance));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Never);
        }

        [Test]
        public void Handle_refresh_by_instance_marks_routes_dirty_when_content_affects_routes()
        {
            IContent changedContent = CreateContent(
                path: "-1,100",
                level: 1,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate);

            ContentCacheRefresherHandler sut = CreateSut([], out Mock<IArticulateRouteRefreshState> routeRefreshState);

            sut.Handle(new ContentCacheRefresherNotification(changedContent, MessageType.RefreshByInstance));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Once);
        }

        [Test]
        public void Handle_refresh_by_instance_ignores_unpublished_content()
        {
            IContent changedContent = CreateContent(
                path: "-1,100",
                level: 1,
                sortOrder: 0,
                alias: ArticulateConstants.ContentType.Articulate,
                published: false);

            ContentCacheRefresherHandler sut = CreateSut([], out Mock<IArticulateRouteRefreshState> routeRefreshState);

            sut.Handle(new ContentCacheRefresherNotification(changedContent, MessageType.RefreshByInstance));

            routeRefreshState.Verify(x => x.MarkDirty(), Times.Never);
        }

        private static ContentCacheRefresherHandler CreateSut(
            IEnumerable<IPublishedContent> articulateRoots,
            out Mock<IArticulateRouteRefreshState> routeRefreshState,
            Umbraco.Cms.Core.Web.IUmbracoContextAccessor? umbracoContextAccessor = null,
            Umbraco.Cms.Infrastructure.Scoping.IScopeProvider? scopeProvider = null)
        {
            routeRefreshState = new Mock<IArticulateRouteRefreshState>();

            Mock<IPublishedContentTypeCache> contentTypeCache = new();
            contentTypeCache
                .Setup(x => x.Get(PublishedItemType.Content, ArticulateConstants.ContentType.Articulate))
                .Returns(Mock.Of<IPublishedContentType>());

            Mock<IDocumentCacheService> documentCacheService = new();
            documentCacheService
                .Setup(x => x.GetByContentType(It.IsAny<IPublishedContentType>()))
                .Returns(articulateRoots);

            return new ContentCacheRefresherHandler(
                umbracoContextAccessor ?? Mock.Of<Umbraco.Cms.Core.Web.IUmbracoContextAccessor>(),
                routeRefreshState.Object,
                contentTypeCache.Object,
                documentCacheService.Object,
                scopeProvider ?? CreateScopeProvider(),
                NullLogger<ContentCacheRefresherHandler>.Instance);
        }

        private static Umbraco.Cms.Infrastructure.Scoping.IScopeProvider CreateScopeProvider()
        {
            Mock<Umbraco.Cms.Infrastructure.Scoping.IScopeProvider> scopeProvider = new();
            scopeProvider
                .Setup(x => x.CreateScope(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<RepositoryCacheMode>(), It.IsAny<Umbraco.Cms.Core.Events.IEventDispatcher?>(), It.IsAny<Umbraco.Cms.Core.Events.IScopedNotificationPublisher?>(), It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns(Mock.Of<Umbraco.Cms.Infrastructure.Scoping.IScope>());
            return scopeProvider.Object;
        }

        private static Umbraco.Cms.Core.Web.IUmbracoContextAccessor CreateUmbracoContextAccessor(
            IPublishedContent? liveContent = null,
            IPublishedContent? previewContent = null,
            IPublishedContentCache? publishedContentCache = null)
        {
            Mock<IPublishedContentCache> contentCache = publishedContentCache is null
                ? new Mock<IPublishedContentCache>()
                : Mock.Get(publishedContentCache);

            if (publishedContentCache is null)
            {
                if (liveContent is not null)
                {
                    contentCache.Setup(x => x.GetById(liveContent.Id)).Returns(liveContent);
                }

                if (previewContent is not null)
                {
                    contentCache.Setup(x => x.GetById(true, previewContent.Id)).Returns(previewContent);
                }
            }

            Mock<Umbraco.Cms.Core.Web.IUmbracoContext> umbracoContext = new();
            umbracoContext.SetupGet(x => x.Content).Returns(contentCache.Object);

            Mock<Umbraco.Cms.Core.Web.IUmbracoContextAccessor> umbracoContextAccessor = new();
            Umbraco.Cms.Core.Web.IUmbracoContext? context = umbracoContext.Object;
            umbracoContextAccessor
                .Setup(x => x.TryGetUmbracoContext(out context))
                .Returns(true);

            return umbracoContextAccessor.Object;
        }

        private static IContent CreateContent(string path, int level, int sortOrder, string alias, bool published = true)
        {
            Mock<ISimpleContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(alias);

            Mock<IContent> content = new();
            content.SetupGet(x => x.Path).Returns(path);
            content.SetupGet(x => x.Level).Returns(level);
            content.SetupGet(x => x.SortOrder).Returns(sortOrder);
            content.SetupGet(x => x.Published).Returns(published);
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            return content.Object;
        }

        private static IPublishedContent CreatePublishedContent(int id, string path, int level, int sortOrder, string alias)
        {
            Mock<IPublishedContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(alias);

            Mock<IPublishedContent> content = new();
            content.SetupGet(x => x.Id).Returns(id);
            content.SetupGet(x => x.Path).Returns(path);
            content.SetupGet(x => x.Level).Returns(level);
            content.SetupGet(x => x.SortOrder).Returns(sortOrder);
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            return content.Object;
        }
    }
}
