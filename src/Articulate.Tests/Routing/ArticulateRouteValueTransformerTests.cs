#nullable enable
using System.Data;
using Articulate.Routing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Cms.Web.Website.Routing;
using IScopeProvider = Umbraco.Cms.Infrastructure.Scoping.IScopeProvider;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRouteValueTransformerTests
    {
        [Test]
        public async Task TransformAsync_builds_route_cache_for_first_eligible_request()
        {
            (ArticulateRouteValueTransformer sut, ArticulateRouter router, _) = CreateSut();
            object initialRouteCache = router.RouteCache;

            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());

            Assert.That(router.RouteCache, Is.Not.SameAs(initialRouteCache));
        }

        [Test]
        public async Task TransformAsync_reuses_route_cache_when_refresh_version_is_unchanged()
        {
            (ArticulateRouteValueTransformer sut, ArticulateRouter router, _) = CreateSut();

            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());
            object builtRouteCache = router.RouteCache;

            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());

            Assert.That(router.RouteCache, Is.SameAs(builtRouteCache));
        }

        [Test]
        public async Task TransformAsync_rebuilds_route_cache_once_after_routes_are_marked_dirty()
        {
            (ArticulateRouteValueTransformer sut, ArticulateRouter router, ArticulateRouteRefreshState refreshState) =
                CreateSut();

            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());
            object initialBuiltRouteCache = router.RouteCache;

            refreshState.MarkDirty();
            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());
            object rebuiltRouteCache = router.RouteCache;
            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());

            Assert.That(rebuiltRouteCache, Is.Not.SameAs(initialBuiltRouteCache));
            Assert.That(router.RouteCache, Is.SameAs(rebuiltRouteCache));
        }

        // With no Articulate roots seeded in CreateSut(), MapRoutes builds an empty cache, so the
        // reference-identity tests above are really "empty rebuild" assertions. These two pin that
        // contract: empty when no roots, and a dirty rebuild swaps in a fresh dictionary (doesn't
        // mutate the existing one — which would silently break dirty-detection).
        [Test]
        public async Task TransformAsync_builds_empty_route_cache_when_no_articulate_roots_exist()
        {
            (ArticulateRouteValueTransformer sut, ArticulateRouter router, _) = CreateSut();

            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());

            Assert.That(router.RouteCache, Is.Empty);
        }

        [Test]
        public async Task TransformAsync_rebuild_produces_fresh_empty_cache_after_dirty()
        {
            (ArticulateRouteValueTransformer sut, ArticulateRouter router, ArticulateRouteRefreshState refreshState) =
                CreateSut();

            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());
            var initialCache = (System.Collections.Concurrent.ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache>)router.RouteCache;

            refreshState.MarkDirty();
            await sut.TransformAsync(CreateHttpContext(), new RouteValueDictionary());
            var rebuiltCache = (System.Collections.Concurrent.ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache>)router.RouteCache;

            Assert.Multiple(() =>
            {
                Assert.That(rebuiltCache, Is.Not.SameAs(initialCache));
                Assert.That(rebuiltCache, Is.Empty);
                Assert.That(initialCache, Is.Empty);
            });
        }

        private static (
            ArticulateRouteValueTransformer Transformer,
            ArticulateRouter Router,
            ArticulateRouteRefreshState RefreshState) CreateSut()
        {
            Mock<IScopeProvider> scopeProvider = new();
            scopeProvider
                .Setup(x => x.CreateCoreScope(
                    It.IsAny<IsolationLevel>(),
                    It.IsAny<RepositoryCacheMode>(),
                    It.IsAny<IEventDispatcher?>(),
                    It.IsAny<IScopedNotificationPublisher?>(),
                    It.IsAny<bool?>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()))
                .Returns(Mock.Of<ICoreScope>());

            ArticulateRouter router = new(
                Mock.Of<IControllerActionSearcher>(),
                scopeProvider.Object,
                NullLogger<ArticulateRouter>.Instance
#if UMBRACO_18_OR_GREATER
                , Mock.Of<IDocumentUrlService>()
                , Mock.Of<Umbraco.Cms.Core.Services.Navigation.IDocumentNavigationQueryService>()
                , Mock.Of<Umbraco.Cms.Core.Services.Navigation.IPublishedContentStatusFilteringService>()
#endif
            );

            IPublishedContentType articulateContentType = Mock.Of<IPublishedContentType>();
            IPublishedContentTypeCache publishedContentTypeCache =
                Mock.Of<IPublishedContentTypeCache>(x =>
                    x.Get(PublishedItemType.Content, ArticulateConstants.ContentType.Articulate) ==
                    articulateContentType);
            IDocumentCacheService documentCacheService =
                Mock.Of<IDocumentCacheService>(x =>
                    x.GetByContentType(articulateContentType) == Array.Empty<IPublishedContent>());

            IUmbracoContext umbracoContext = Mock.Of<IUmbracoContext>(x =>
                x.Domains == Mock.Of<IDomainCache>(domains =>
                    domains.GetAll(false) == Array.Empty<Domain>()));
            Mock<IUmbracoContextAccessor> umbracoContextAccessor = new();
            IUmbracoContext? context = umbracoContext;
            umbracoContextAccessor
                .Setup(x => x.TryGetUmbracoContext(out context))
                .Returns(true);

            ArticulateRouteRefreshState refreshState = new();
            UmbracoRouteValueTransformer umbracoTransformer = CreateUmbracoTransformer(umbracoContextAccessor.Object);

            ArticulateRouteValueTransformer transformer = new(
                Mock.Of<IRuntimeState>(x => x.Level == RuntimeLevel.Run),
                umbracoContextAccessor.Object,
                Mock.Of<IPublishedRouter>(),
                Mock.Of<IRoutableDocumentFilter>(x => x.IsDocumentRequest(It.IsAny<string>()) == true),
                router,
                refreshState,
                NullLogger<ArticulateRouteValueTransformer>.Instance,
                umbracoTransformer,
                publishedContentTypeCache,
                documentCacheService);

            return (transformer, router, refreshState);
        }

        private static DefaultHttpContext CreateHttpContext()
        {
            DefaultHttpContext httpContext = new();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Path = "/dynamic-route";

            IPublishedRequest publishedRequest = Mock.Of<IPublishedRequest>(x =>
                x.PublishedContent == null &&
                x.ResponseStatusCode == 404);
            httpContext.Features.Set(new UmbracoRouteValues(publishedRequest, null!));

            return httpContext;
        }

        private static UmbracoRouteValueTransformer CreateUmbracoTransformer(
            IUmbracoContextAccessor umbracoContextAccessor) =>
            new(
                NullLogger<UmbracoRouteValueTransformer>.Instance,
                umbracoContextAccessor,
                Mock.Of<IPublishedRouter>(),
                Mock.Of<IRuntimeState>(x => x.Level == RuntimeLevel.Run),
                Mock.Of<IUmbracoRouteValuesFactory>(),
                Mock.Of<IRoutableDocumentFilter>(),
                Mock.Of<IDataProtectionProvider>(),
                Mock.Of<IControllerActionSearcher>(),
                Mock.Of<IPublicAccessRequestHandler>(),
                Mock.Of<IUmbracoVirtualPageRoute>(),
                Mock.Of<IOptionsMonitor<GlobalSettings>>(x => x.CurrentValue == new GlobalSettings()),
                Mock.Of<IDocumentUrlService>());
    }
}
