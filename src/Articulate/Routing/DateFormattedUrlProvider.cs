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

namespace Articulate.Routing
{
    /// <summary>
    /// Provides date-formatted URLs for Articulate blog posts (e.g., /YYYY/MM/DD/post-name/).
    /// </summary>
#if NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER
    public class DateFormattedUrlProvider : DefaultUrlProvider
#else
    public class DateFormattedUrlProvider : NewDefaultUrlProvider
#endif
    {
#if NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER
        private readonly IDocumentUrlService _documentUrlService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DateFormattedUrlProvider"/> class for NET10 (Umbraco 17+).
        /// </summary>
        public DateFormattedUrlProvider(
            IOptionsMonitor<RequestHandlerSettings> requestSettings,
            ILogger<DefaultUrlProvider> logger,
            ISiteDomainMapper siteDomainMapper,
            IUmbracoContextAccessor umbracoContextAccessor,
            UriUtility uriUtility,
            IPublishedContentCache publishedContentCache,
            IDomainCache domainCache,
            IIdKeyMap idKeyMap,
            IDocumentUrlService documentUrlService,
            IDocumentNavigationQueryService navigationQueryService,
            IPublishedContentStatusFilteringService publishedContentStatusFilteringService,
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
            _documentUrlService = documentUrlService;
        }
#else
        /// <summary>
        /// Initializes a new instance of the <see cref="DateFormattedUrlProvider"/> class for NET9 (Umbraco 16).
        /// </summary>
        public DateFormattedUrlProvider(
            IOptionsMonitor<RequestHandlerSettings> requestSettings,
#if NET9_0
            ILogger<DefaultUrlProvider> logger,
#else
            ILogger<NewDefaultUrlProvider> logger,
#endif
            ISiteDomainMapper siteDomainMapper,
            IUmbracoContextAccessor umbracoContextAccessor,
            UriUtility uriUtility,
#if NET9_0
#pragma warning disable CS0618 // Type or member is obsolete
            ILocalizationService localizationService,
#pragma warning restore CS0618 // Type or member is obsolete
#endif
            IPublishedContentCache publishedContentCache,
            IDomainCache domainCache,
            IIdKeyMap idKeyMap,
            IDocumentUrlService documentUrlService,
            IDocumentNavigationQueryService navigationQueryService,
            IPublishedContentStatusFilteringService publishedContentStatusFilteringService,
            ILanguageService languageService)
            : base(
                requestSettings,
                logger,
                siteDomainMapper,
                umbracoContextAccessor,
                uriUtility,
#if NET9_0
                localizationService,
#endif
                publishedContentCache,
                domainCache,
                idKeyMap,
                documentUrlService,
                navigationQueryService,
                publishedContentStatusFilteringService,
                languageService)
        {
        }
#endif


        /// <inheritdoc/>
        public override UrlInfo? GetUrl(IPublishedContent content, UrlMode mode, string? culture, Uri current)
        {
            if (content is
                    not
                    {
                        ContentType.Alias: ArticulateConstants.ContentType.ArticulateRichText
                        or ArticulateConstants.ContentType.ArticulateMarkdown
                    }
                || content.Parent() is null)
            {
                return null;
            }

            if (content.Parent()?.Parent() is not null)
            {
                var useDateFormat = content.Parent()?.Parent()?.Value<bool>("useDateFormatForUrl") ?? false;
                if (!useDateFormat)
                {
                    return null;
                }
            }

            DateTime? date = content.Value<DateTime?>("publishedDate");
            if (date is null)
            {
                return null;
            }

            var urlFolder = $"{date.Value.Year}/{date.Value.Month:d2}/{date.Value.Day:d2}";
            IPublishedContent? parent = content.Parent();
            if (parent is null)
            {
                return null;
            }
#if NET10_0_OR_GREATER
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

#else
            UrlInfo? parentPath = base.GetUrl(parent, mode, culture, current);
            var parentUrl = parentPath?.Text.EnsureEndsWith("/");
            if (string.IsNullOrWhiteSpace(parentUrl) || string.IsNullOrWhiteSpace(content.UrlSegment))
            {
                return null;
            }

            var newUrl = parentUrl + urlFolder + "/" + content.UrlSegment?.EnsureEndsWith("/");
            return UrlInfo.Url(newUrl, culture);
#endif
        }
    }
}
