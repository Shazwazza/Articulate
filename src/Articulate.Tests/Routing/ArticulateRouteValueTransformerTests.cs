#nullable enable
using System.Data;
using Articulate.Routing;
using Examine;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Cms.Web.Website.Routing;
using IScopeProvider = Umbraco.Cms.Infrastructure.Scoping.IScopeProvider;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRouteValueTransformerTests
    {
        private IServiceProvider _originalServiceProvider = null!;
        private ServiceProvider _testServiceProvider = null!;

        [OneTimeSetUp]
        public void SetUp()
        {
            _originalServiceProvider = StaticServiceProvider.Instance;

            var publishedUrlProvider = new Mock<IPublishedUrlProvider>();
            publishedUrlProvider
                .Setup(x => x.GetUrl(
                    It.IsAny<IPublishedContent>(),
                    It.IsAny<UrlMode>(),
                    It.IsAny<string?>(),
                    It.IsAny<Uri?>()))
                .Returns((IPublishedContent content, UrlMode _, string? _, Uri? _) =>
                    content.Id == 100 ? "/blog/" : "/news/");

            _testServiceProvider = new ServiceCollection()
                .AddSingleton(publishedUrlProvider.Object)
                .AddSingleton(Mock.Of<IVariationContextAccessor>())
                .AddSingleton(Mock.Of<IDomainCache>())
                .AddSingleton(Mock.Of<IPublishedContentCache>())
                .AddSingleton(Mock.Of<IMediaNavigationQueryService>())
                .AddSingleton(Mock.Of<IPublishedModelFactory>())
                .AddSingleton(Mock.Of<IDocumentNavigationQueryService>())
                .AddSingleton(Mock.Of<IPublishedContentStatusFilteringService>())
                .AddSingleton(Mock.Of<IPublishedValueFallback>())
                .AddSingleton(Mock.Of<IUserService>())
                .AddSingleton(Mock.Of<IUmbracoContextAccessor>())
                .AddSingleton(Mock.Of<ISiteDomainMapper>())
                .AddSingleton(Mock.Of<ITemplateService>())
                .AddSingleton(Mock.Of<IContentTypeService>())
                .AddSingleton(Mock.Of<IMediaTypeService>())
                .AddSingleton(Mock.Of<IMemberTypeService>())
#if !UMBRACO_18_OR_GREATER
                .AddSingleton(Mock.Of<IFileService>())
#endif
                .AddSingleton(Microsoft.Extensions.Options.Options.Create(new WebRoutingSettings()))
                .AddSingleton(Mock.Of<IExamineManager>())
                .AddSingleton(Mock.Of<IDocumentUrlService>())
                .BuildServiceProvider();

            StaticServiceProvider.Instance = _testServiceProvider;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _testServiceProvider.Dispose();
            StaticServiceProvider.Instance = _originalServiceProvider;
        }

        [Test]
        public async Task TransformAsync_routes_seeded_articulate_path_to_controller_and_content()
        {
            IPublishedContent root = CreateRoot(100);
            (ArticulateRouteValueTransformer sut, _, _) = CreateSut([root]);
            DefaultHttpContext context = CreateHttpContext("/blog/rss");

            RouteValueDictionary values = await sut.TransformAsync(context, new RouteValueDictionary());

            UmbracoRouteValues routeValues = context.Features.Get<UmbracoRouteValues>()!;
            Assert.Multiple(() =>
            {
                Assert.That(values["controller"], Is.EqualTo("ArticulateRss"));
                Assert.That(values["action"], Is.EqualTo("Index"));
                Assert.That(routeValues.PublishedRequest.PublishedContent, Is.SameAs(root));
            });
        }

        [Test]
        public async Task TransformAsync_reuses_route_cache_when_refresh_version_is_unchanged()
        {
            IPublishedContent root = CreateRoot(100);
            (ArticulateRouteValueTransformer sut, ArticulateRouter router, _) = CreateSut([root]);

            _ = await sut.TransformAsync(CreateHttpContext("/blog/rss"), new RouteValueDictionary());
            object builtRouteCache = router.RouteCache;

            RouteValueDictionary values = await sut.TransformAsync(
                CreateHttpContext("/blog/rss"),
                new RouteValueDictionary());

            Assert.Multiple(() =>
            {
                Assert.That(values["controller"], Is.EqualTo("ArticulateRss"));
                Assert.That(router.RouteCache, Is.SameAs(builtRouteCache));
            });
        }

        [Test]
        public async Task TransformAsync_rebuilds_route_cache_after_routes_are_marked_dirty()
        {
            var roots = new List<IPublishedContent> { CreateRoot(100) };
            (ArticulateRouteValueTransformer sut, ArticulateRouter router, ArticulateRouteRefreshState refreshState) =
                CreateSut(roots);

            _ = await sut.TransformAsync(CreateHttpContext("/blog/rss"), new RouteValueDictionary());
            object initialBuiltRouteCache = router.RouteCache;

            IPublishedContent newRoot = CreateRoot(200);
            roots.Add(newRoot);
            refreshState.MarkDirty();

            DefaultHttpContext context = CreateHttpContext("/news/rss");
            RouteValueDictionary values = await sut.TransformAsync(context, new RouteValueDictionary());

            Assert.Multiple(() =>
            {
                Assert.That(router.RouteCache, Is.Not.SameAs(initialBuiltRouteCache));
                Assert.That(values["controller"], Is.EqualTo("ArticulateRss"));
                Assert.That(values["action"], Is.EqualTo("Index"));
                Assert.That(
                    context.Features.Get<UmbracoRouteValues>()!.PublishedRequest.PublishedContent,
                    Is.SameAs(newRoot));
            });
        }

        private static (
            ArticulateRouteValueTransformer Transformer,
            ArticulateRouter Router,
            ArticulateRouteRefreshState RefreshState) CreateSut(
            IEnumerable<IPublishedContent> articulateRoots)
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

            Mock<IControllerActionSearcher> controllerActionSearcher = new();
            controllerActionSearcher
                .Setup(x => x.Find<IRenderController>(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()))
                .Returns((HttpContext _, string? controller, string? action) => new ControllerActionDescriptor
                {
                    ControllerName = controller ?? string.Empty,
                    ActionName = action ?? string.Empty
                });

            Mock<IOptions<Articulate.Options.ArticulateOptions>> articulateOptions = new();
            articulateOptions
                .SetupGet(x => x.Value)
                .Returns(new Articulate.Options.ArticulateOptions { EnableMetaWeblog = false });

            ArticulateRouter router = new(
                controllerActionSearcher.Object,
                scopeProvider.Object,
#if UMBRACO_18_OR_GREATER
                NullLogger<ArticulateRouter>.Instance,
                Mock.Of<IDocumentUrlService>(),
                articulateOptions.Object
#else
                NullLogger<ArticulateRouter>.Instance,
                articulateOptions.Object
#endif
            );

            IPublishedContentType articulateContentType = Mock.Of<IPublishedContentType>(x =>
                x.ItemType == PublishedItemType.Content &&
                x.Alias == ArticulateConstants.ContentType.Articulate);
            Mock<IPublishedContentTypeCache> publishedContentTypeCache = new();
            publishedContentTypeCache
                .Setup(x => x.Get(PublishedItemType.Content, ArticulateConstants.ContentType.Articulate))
                .Returns(articulateContentType);

            Mock<IDocumentCacheService> documentCacheService = new();
            documentCacheService
                .Setup(x => x.GetByContentType(articulateContentType))
                .Returns(() => articulateRoots);

            Mock<IPublishedContentCache> publishedContentCache = new();
            publishedContentCache
                .Setup(x => x.GetById(It.IsAny<int>()))
                .Returns((int id) => articulateRoots.FirstOrDefault(root => root.Id == id));

            Mock<IUmbracoContext> umbracoContext = new();
            umbracoContext.SetupGet(x => x.CleanedUmbracoUrl).Returns(new Uri("https://example.com/"));
            umbracoContext
                .SetupGet(x => x.Domains)
                .Returns(Mock.Of<IDomainCache>(domains => domains.GetAll(false) == Array.Empty<Domain>()));
            umbracoContext.SetupGet(x => x.Content).Returns(publishedContentCache.Object);

            Mock<IUmbracoContextAccessor> umbracoContextAccessor = new();
            IUmbracoContext? context = umbracoContext.Object;
            umbracoContextAccessor
                .Setup(x => x.TryGetUmbracoContext(out context))
                .Returns(true);

            Mock<IPublishedRouter> publishedRouter = new();
            publishedRouter
                .Setup(x => x.CreateRequestAsync(It.IsAny<Uri>()))
                .ReturnsAsync((Uri uri) =>
#if UMBRACO_18_OR_GREATER
                    new PublishedRequestBuilder(uri, Mock.Of<ITemplateService>()));
#else
                    new PublishedRequestBuilder(uri, Mock.Of<IFileService>()));
#endif

            ArticulateRouteRefreshState refreshState = new();
            UmbracoRouteValueTransformer umbracoTransformer = CreateUmbracoTransformer(umbracoContextAccessor.Object);

            ArticulateRouteValueTransformer transformer = new(
                Mock.Of<IRuntimeState>(x => x.Level == RuntimeLevel.Run),
                umbracoContextAccessor.Object,
                publishedRouter.Object,
                Mock.Of<IRoutableDocumentFilter>(x => x.IsDocumentRequest(It.IsAny<string>()) == true),
                router,
                refreshState,
                NullLogger<ArticulateRouteValueTransformer>.Instance,
                umbracoTransformer,
                publishedContentTypeCache.Object,
                documentCacheService.Object);

            return (transformer, router, refreshState);
        }

        private static DefaultHttpContext CreateHttpContext(string path)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("example.com");
            httpContext.Request.Path = path;

            IPublishedRequest publishedRequest = Mock.Of<IPublishedRequest>(x =>
                x.PublishedContent == null &&
                x.ResponseStatusCode == 404);
            httpContext.Features.Set(new UmbracoRouteValues(publishedRequest, null!));

            return httpContext;
        }

        private static IPublishedContent CreateRoot(int id)
        {
            Mock<IPublishedContentType> contentType = new();
            contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);
            contentType.SetupGet(x => x.Alias).Returns(ArticulateConstants.ContentType.Articulate);

            Mock<IPublishedContent> root = new();
            root.SetupGet(x => x.Id).Returns(id);
            root.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            root.SetupGet(x => x.Name).Returns(id == 100 ? "Blog" : "News");
            root.SetupGet(x => x.Path).Returns($"-1,{id}");
            root.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);
            root.SetupGet(x => x.ContentType).Returns(contentType.Object);
            root.Setup(x => x.GetProperty(It.IsAny<string>())).Returns((IPublishedProperty?)null);
            return root.Object;
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
                Mock.Of<IDocumentUrlService>(),
                Mock.Of<IContentRoutingReadiness>(x => x.IsReady == true));
    }
}
