#nullable enable
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Articulate.Routing
{
    /// <summary>
    /// Content finder that handles date-formatted URLs for Articulate blog posts (e.g., /YYYY/MM/DD/post-name/).
    /// </summary>
    public class DateFormattedPostContentFinder : ContentFinderByUrlNew
    {
        private readonly IDocumentUrlService _documentUrlService;
        private readonly IPublishedContentCache _publishedContentCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="DateFormattedPostContentFinder"/> class.
        /// </summary>
        public DateFormattedPostContentFinder(
            ILogger<DateFormattedPostContentFinder> logger,
            IUmbracoContextAccessor umbracoContextAccessor,
            IDocumentUrlService documentUrlService,
            IPublishedContentCache publishedContentCache,
            IOptionsMonitor<WebRoutingSettings> webRoutingSettings)
            : base(logger, umbracoContextAccessor, documentUrlService, publishedContentCache, webRoutingSettings)
        {
            _documentUrlService = documentUrlService;
            _publishedContentCache = publishedContentCache;
        }

        /// <inheritdoc/>
        public override Task<bool> TryFindContent(IPublishedRequestBuilder contentRequest)
        {
            var segmentLength = contentRequest.Uri.Segments.Length;
            if (segmentLength <= 4)
            {
                return Task.FromResult(false);
            }

            if (!TryParseDateFromSegments(contentRequest.Uri.Segments, segmentLength, out DateTime postDate))
            {
                return Task.FromResult(false);
            }

            var newRoute = BuildRouteWithoutDateSegments(contentRequest, segmentLength);
            IPublishedContent? node = FindContentByRoute(contentRequest, newRoute);

            if (!ValidateArticulatePost(node, postDate))
            {
                return Task.FromResult(false);
            }

            _ = contentRequest.SetPublishedContent(node!);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Finds content by route without the date segments.
        /// </summary>
        private IPublishedContent? FindContentByRoute(IPublishedRequestBuilder contentRequest, string route)
        {
            if (!UmbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext))
            {
                return null;
            }

            // Extract the route path (without domain ID prefix if present)
            var routePath = route;
            if (contentRequest.Domain is not null && route.Contains('/'))
            {
                var pos = route.IndexOf('/', StringComparison.Ordinal);
                routePath = route[pos..];
            }

            // Use IDocumentUrlService to find the document key by route
            Guid? documentKey = _documentUrlService.GetDocumentKeyByRoute(
                routePath,
                contentRequest.Culture,
                contentRequest.Domain?.ContentId,
                umbracoContext.InPreviewMode);

            if (documentKey is null)
            {
                return null;
            }

            // Retrieve the published content by key
            IPublishedContent? node = _publishedContentCache.GetById(umbracoContext.InPreviewMode, (Guid)documentKey);
            if (node is not null)
            {
                contentRequest.SetPublishedContent(node);
            }

            return node;
        }

        internal static bool TryParseDateFromSegments(string[] segments, int segmentLength, out DateTime postDate)
        {
            // Fast bail-out: year segment must start with a digit.
            // Covers ~99% of non-date URLs without any string allocation.
            ReadOnlySpan<char> yearSegment = segments[segmentLength - 4].AsSpan();
            if (yearSegment.Length < 5 || !(yearSegment[0] is >= '0' and <= '9'))
            {
                postDate = default;
                return false;
            }

            var stringDate = segments[segmentLength - 4] + segments[segmentLength - 3] +
                             segments[segmentLength - 2].TrimEnd('/');
            return DateTime.TryParseExact(
                stringDate,
                "yyyy/MM/dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out postDate);
        }

        internal static string BuildRouteWithoutDateSegments(IPublishedRequestBuilder contentRequest, int segmentLength)
        {
            var uriSegments = contentRequest.Uri.Segments;
            var sb = new StringBuilder();
            for (var i = 0; i < segmentLength; i++)
            {
                if (i < segmentLength - 4 || i > segmentLength - 2)
                {
                    sb.Append(uriSegments[i].ToLowerInvariant());
                }
            }

            var newRoute = sb.ToString();

            // if there's a domain attached we need to look up the content with the domain ID
            // and the domain's path stripped from the start
            if (contentRequest.HasDomain() && contentRequest.Domain?.Uri is not null)
            {
                DomainAndUri domain = contentRequest.Domain;
                Uri uri = domain.Uri;
                newRoute = domain.ContentId + DomainUtilities.PathRelativeToDomain(uri, newRoute);
            }

            return newRoute;
        }

        private static bool ValidateArticulatePost(IPublishedContent? node, DateTime postDate)
        {
            if (node is null)
            {
                return false;
            }

            if (node.ContentType.Alias != ArticulateConstants.ContentType.ArticulateRichText
                && node.ContentType.Alias != ArticulateConstants.ContentType.ArticulateMarkdown)
            {
                return false;
            }

            var useDateFormat = node.Parent()?.Parent()?.Value<bool?>("useDateFormatForUrl");
            if (useDateFormat != true)
            {
                return false;
            }

            return node.Value<DateTime>("publishedDate").Date == postDate.Date;
        }
    }
}
