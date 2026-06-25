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
    /// <summary>
    ///     Covers the content-type eligibility guard in
    ///     <see cref="DateFormattedUrlProvider.GetUrl" />: only ArticulateMarkdown / ArticulateRichText
    ///     posts are candidates for date-formatted URLs; everything else short-circuits to null before
    ///     any navigation lookup.
    /// </summary>
    /// <remarks>
    ///     The remaining guards (parent-chain, publishedDate, useDateFormatForUrl) and the positive
    ///     URL-build path all read <c>content.Parent()</c> — an extension method that resolves via the
    ///     Umbraco navigation services, not a mockable property. They need an integration test.
    /// </remarks>
    [TestFixture]
    public class DateFormattedUrlProviderTests
    {
        private static readonly Uri _currentUri = new("https://example.com", UriKind.Absolute);

        // Non-Articulate content types short-circuit at the content-type guard before Parent() is
        // ever evaluated (|| short-circuits on the true branch). These are the cleanly unit-testable
        // cases — no navigation context needed.
        [TestCase("article")]
        [TestCase("blogPost")]
        [TestCase("Articulate")] // prefix match must not pass; exact alias required
        [TestCase("ArticulateMarkdownPost")]
        public void GetUrl_returns_null_for_non_articulate_content_type(string contentTypeAlias)
        {
            IPublishedContent content = CreateContent(contentTypeAlias);
            DateFormattedUrlProvider sut = CreateProvider();

            UrlInfo? result = sut.GetUrl(content, UrlMode.Default, culture: null, _currentUri);

            Assert.That(result, Is.Null);
        }

        // Full base-class dependency chain so the provider is instantiable. The guard under test
        // returns before any of these are used, so plain Mock.Of<T>() suffices.
        private static DateFormattedUrlProvider CreateProvider()
        {
            Mock<IHostingEnvironment> hostingEnv = new();
            hostingEnv.Setup(x => x.ApplicationVirtualPath).Returns("/");
            var uriUtility = new UriUtility(hostingEnv.Object);

            Mock<IOptionsMonitor<RequestHandlerSettings>> optionsMonitor = new();
            optionsMonitor.SetupGet(x => x.CurrentValue).Returns(new RequestHandlerSettings());

            return new DateFormattedUrlProvider(
                optionsMonitor.Object,
#if UMBRACO_18_OR_GREATER
                Mock.Of<ILogger<DefaultUrlProvider>>(),
#else
                Mock.Of<ILogger<NewDefaultUrlProvider>>(),
#endif
                Mock.Of<ISiteDomainMapper>(),
                Mock.Of<IUmbracoContextAccessor>(),
                uriUtility,
                Mock.Of<IPublishedContentCache>(),
                Mock.Of<IDomainCache>(),
                Mock.Of<IIdKeyMap>(),
                Mock.Of<IDocumentUrlService>(),
                Mock.Of<IDocumentNavigationQueryService>(),
                Mock.Of<IPublishedContentStatusFilteringService>(),
                Mock.Of<ILanguageService>());
        }

        // Minimal IPublishedContent with only ContentType.Alias set. Parent()/Value<T>() are NOT
        // configured — the content-type guard must short-circuit before they are read.
        private static IPublishedContent CreateContent(string contentTypeAlias)
        {
            Mock<IPublishedContent> content = new();
            Mock<IPublishedContentType> contentType = new();
            contentType.SetupGet(x => x.Alias).Returns(contentTypeAlias);
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            return content.Object;
        }
    }
}
