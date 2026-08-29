#nullable enable
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    /// <summary>
    /// Provides date-formatted URLs for Articulate blog posts (e.g., /YYYY/MM/DD/post-name/).
    /// </summary>
#if UMBRACO_18_OR_GREATER
    public class DateFormattedUrlProvider : DefaultUrlProvider
#else
    public class DateFormattedUrlProvider : NewDefaultUrlProvider
#endif
    {
        private readonly IDocumentNavigationQueryService _navigationQueryService;
        private readonly IPublishedContentStatusFilteringService _publishedContentStatusFilteringService;
        private readonly IPublishedValueFallback _publishedValueFallback;
#if UMBRACO_18_OR_GREATER
        private readonly IDocumentUrlService _documentUrlService;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="DateFormattedUrlProvider"/> class.
        /// </summary>
        public DateFormattedUrlProvider(
            IOptionsMonitor<RequestHandlerSettings> requestSettings,
#if UMBRACO_18_OR_GREATER
            ILogger<DefaultUrlProvider> logger,
#else
            ILogger<NewDefaultUrlProvider> logger,
#endif
            ISiteDomainMapper siteDomainMapper,
            IUmbracoContextAccessor umbracoContextAccessor,
            UriUtility uriUtility,
            IPublishedContentCache publishedContentCache,
            IDomainCache domainCache,
            IIdKeyMap idKeyMap,
            IDocumentUrlService documentUrlService,
            IDocumentNavigationQueryService navigationQueryService,
            IPublishedContentStatusFilteringService publishedContentStatusFilteringService,
            IPublishedValueFallback publishedValueFallback,
            ILanguageService languageService)
            : base(
                requestSettings,
                logger,
                siteDomainMapper,
                umbracoContextAccessor,
                uriUtility,
                publishedContentCache,
                domainCache,
                idKeyMap,
                documentUrlService,
                navigationQueryService,
                publishedContentStatusFilteringService,
                languageService)
        {
            _navigationQueryService = navigationQueryService;
            _publishedContentStatusFilteringService = publishedContentStatusFilteringService;
            _publishedValueFallback = publishedValueFallback;
#if UMBRACO_18_OR_GREATER
            _documentUrlService = documentUrlService;
#endif
        }

        /// <inheritdoc/>
        public override UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string? culture, Uri current)
        {
            string contentTypeAlias = content.ContentType.Alias;
            if (contentTypeAlias != ArticulateConstants.ContentType.ArticulateRichText &&
                contentTypeAlias != ArticulateConstants.ContentType.ArticulateMarkdown)
            {
                return null;
            }

            IPublishedContent? parent = content.Parent<IPublishedContent>(
                _navigationQueryService,
                _publishedContentStatusFilteringService);
            if (parent is null)
            {
                return null;
            }

            IPublishedContent? root = parent.Parent<IPublishedContent>(
                _navigationQueryService,
                _publishedContentStatusFilteringService);
            if (root is not null && root.Value<bool>(_publishedValueFallback, "useDateFormatForUrl") is false)
            {
                return null;
            }

            DateTime? date = content.Value<DateTime?>(_publishedValueFallback, "publishedDate");
            if (date is null)
            {
                return null;
            }

            var urlFolder = $"{date.Value.Year}/{date.Value.Month:d2}/{date.Value.Day:d2}";
            UrlInfo? parentPath = base.GetUrl(parent, mode, culture, current);
            var parentUrl = parentPath?.Url?.ToString().EnsureEndsWith("/");
#if UMBRACO_18_OR_GREATER
            var urlSegment = _documentUrlService.GetUrlSegment(content.Key, culture ?? string.Empty, false);

#else
            var urlSegment = content.UrlSegment;
#endif
            if (string.IsNullOrWhiteSpace(parentUrl) || string.IsNullOrWhiteSpace(urlSegment))
            {
                return null;
            }

            var newUrl = parentUrl + urlFolder + "/" + urlSegment?.EnsureEndsWith("/");
            return UrlInfo.AsUrl(newUrl, "Articulate.Routing.DateFormattedUrlProvider", culture);
        }
    }
}
