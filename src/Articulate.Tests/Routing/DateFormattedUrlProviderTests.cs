#nullable enable
using Articulate.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class DateFormattedUrlProviderTests
    {
        private static readonly Uri _currentUri = new("https://example.com", UriKind.Absolute);

        private Mock<IDocumentNavigationQueryService> _navigationQueryService = null!;
        private Mock<IPublishedContentStatusFilteringService> _statusFilteringService = null!;
        private Mock<IPublishedValueFallback> _publishedValueFallback = null!;
        private Mock<IDocumentUrlService> _documentUrlService = null!;
        private Mock<IUmbracoContextAccessor> _umbracoContextAccessor = null!;
        private Mock<ILanguageService> _languageService = null!;
        private Mock<IDomainCache> _domainCache = null!;

        [SetUp]
        public void SetUp()
        {
            _navigationQueryService = new Mock<IDocumentNavigationQueryService>();
            _statusFilteringService = new Mock<IPublishedContentStatusFilteringService>();
            _publishedValueFallback = new Mock<IPublishedValueFallback>();
            _documentUrlService = new Mock<IDocumentUrlService>();
            _umbracoContextAccessor = new Mock<IUmbracoContextAccessor>();
            _languageService = new Mock<ILanguageService>();
            _domainCache = new Mock<IDomainCache>();

            Mock<IUmbracoContext> umbracoContext = new();
            umbracoContext.SetupGet(x => x.InPreviewMode).Returns(false);
            IUmbracoContext? context = umbracoContext.Object;
            _umbracoContextAccessor
                .Setup(x => x.TryGetUmbracoContext(out context))
                .Returns(true);

            _languageService
                .Setup(x => x.GetDefaultIsoCodeAsync())
                .ReturnsAsync("en-US");
            _domainCache
                .Setup(x => x.GetAssigned(It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(Array.Empty<Domain>());
        }

        [TestCase("article")]
        [TestCase("blogPost")]
        [TestCase("Articulate")]
        [TestCase("ArticulateMarkdownPost")]
        public void GetUrl_returns_null_for_non_articulate_content_type(string contentTypeAlias)
        {
            IPublishedContent content = CreateContent(contentTypeAlias, Guid.NewGuid(), 1);
            DateFormattedUrlProvider sut = CreateProvider();

            UrlInfo? result = sut.GetUrl(content, UrlMode.Default, culture: null, _currentUri);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetUrl_returns_date_formatted_url_for_articulate_post()
        {
            (IPublishedContent site, IPublishedContent blog, IPublishedContent post) = CreatePostTree(
                useDateFormat: true);
            ConfigureTree(site, blog, post);
            DateFormattedUrlProvider sut = CreateProvider(blog.Key, post.Key);

            UrlInfo? result = sut.GetUrl(post, UrlMode.Auto, culture: null, _currentUri);

            Assert.That(result?.Url?.ToString(), Is.EqualTo("/blog/2024/06/13/my-post/"));
        }

        [Test]
        public void GetUrl_returns_null_when_date_format_is_disabled()
        {
            (IPublishedContent site, IPublishedContent blog, IPublishedContent post) = CreatePostTree(
                useDateFormat: false);
            ConfigureTree(site, blog, post);
            DateFormattedUrlProvider sut = CreateProvider(blog.Key, post.Key);

            UrlInfo? result = sut.GetUrl(post, UrlMode.Auto, culture: null, _currentUri);

            Assert.That(result, Is.Null);
        }

        private DateFormattedUrlProvider CreateProvider(Guid blogKey = default, Guid postKey = default)
        {
            Mock<IHostingEnvironment> hostingEnv = new();
            hostingEnv.Setup(x => x.ApplicationVirtualPath).Returns("/");
            var uriUtility = new UriUtility(hostingEnv.Object);

            Mock<IOptionsMonitor<RequestHandlerSettings>> optionsMonitor = new();
            optionsMonitor
                .SetupGet(x => x.CurrentValue)
                .Returns(new RequestHandlerSettings { AddTrailingSlash = true });

            _documentUrlService
                .Setup(x => x.GetLegacyRouteFormat(blogKey, It.IsAny<string?>(), false))
                .Returns("100/blog");
            _documentUrlService
                .Setup(x => x.GetUrlSegment(postKey, It.IsAny<string>(), false))
                .Returns("my-post");

            return new DateFormattedUrlProvider(
                optionsMonitor.Object,
#if UMBRACO_18_OR_GREATER
                Mock.Of<ILogger<DefaultUrlProvider>>(),
#else
                Mock.Of<ILogger<NewDefaultUrlProvider>>(),
#endif
                Mock.Of<ISiteDomainMapper>(),
                _umbracoContextAccessor.Object,
                uriUtility,
                Mock.Of<IPublishedContentCache>(),
                _domainCache.Object,
                Mock.Of<IIdKeyMap>(),
                _documentUrlService.Object,
                _navigationQueryService.Object,
                _statusFilteringService.Object,
                _publishedValueFallback.Object,
                _languageService.Object);
        }

        private void ConfigureTree(
            IPublishedContent site,
            IPublishedContent blog,
            IPublishedContent post)
        {
            var parentKeys = new Dictionary<Guid, Guid?>
            {
                [post.Key] = blog.Key,
                [blog.Key] = site.Key,
                [site.Key] = null
            };

            _navigationQueryService
                .Setup(x => x.TryGetParentKey(It.IsAny<Guid>(), out It.Ref<Guid?>.IsAny))
                .Callback(new TryGetParentKeyCallback((Guid key, out Guid? parentKey) =>
                    parentKey = parentKeys[key]))
                .Returns(true);

            var contentByKey = new Dictionary<Guid, IPublishedContent>
            {
                [site.Key] = site,
                [blog.Key] = blog,
                [post.Key] = post
            };
#pragma warning disable CS0618 // The explicit Parent overload still uses this compatibility API in the supported Umbraco lanes.
            _statusFilteringService
                .Setup(x => x.Unfiltered(It.IsAny<IEnumerable<Guid>>()))
                .Returns((IEnumerable<Guid> keys) => keys
                    .Select(key => contentByKey.GetValueOrDefault(key))
                    .OfType<IPublishedContent>());
#pragma warning restore CS0618
        }

        private static (IPublishedContent Site, IPublishedContent Blog, IPublishedContent Post) CreatePostTree(
            bool useDateFormat)
        {
            var siteKey = Guid.NewGuid();
            var blogKey = Guid.NewGuid();
            var postKey = Guid.NewGuid();

            IPublishedContent site = CreateContent(
                "site",
                siteKey,
                100,
                ("useDateFormatForUrl", useDateFormat));
            IPublishedContent blog = CreateContent("blog", blogKey, 101);
            IPublishedContent post = CreateContent(
                ArticulateConstants.ContentType.ArticulateMarkdown,
                postKey,
                102,
                ("publishedDate", new DateTime(2024, 6, 13)));
#if !UMBRACO_18_OR_GREATER
            Mock.Get(post).SetupGet(x => x.UrlSegment).Returns("my-post");
#endif

            return (site, blog, post);
        }

        private static IPublishedContent CreateContent(
            string contentTypeAlias,
            Guid key,
            int id,
            params (string Alias, object Value)[] values)
        {
            Mock<IPublishedContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(contentTypeAlias);
            contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);

            Dictionary<string, IPublishedProperty> properties = values.ToDictionary(
                value => value.Alias,
                value => CreateProperty(value.Value));
            Mock<IPublishedContent> content = new();
            content.SetupGet(x => x.Key).Returns(key);
            content.SetupGet(x => x.Id).Returns(id);
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            content
                .Setup(x => x.GetProperty(It.IsAny<string>()))
                .Returns((string alias) => properties.GetValueOrDefault(alias));
            return content.Object;
        }

        private static IPublishedProperty CreateProperty(object value)
        {
            Mock<IPublishedProperty> property = new();
            property
                .Setup(x => x.HasValue(It.IsAny<string?>(), It.IsAny<string?>()))
                .Returns(true);
            property
                .Setup(x => x.GetValue(It.IsAny<string?>(), It.IsAny<string?>()))
                .Returns(value);
            return property.Object;
        }

        private delegate void TryGetParentKeyCallback(Guid key, out Guid? parentKey);
    }
}
