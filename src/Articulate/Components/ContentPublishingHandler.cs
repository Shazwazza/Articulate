#nullable enable
using Articulate.Routing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Articulate.Components
{
    /// <summary>
    /// Validates Articulate routing configuration before publish so editors see backoffice validation errors.
    /// </summary>
    internal sealed class ContentPublishingHandler(
        IContentService contentService,
        IDomainCacheService domainCacheService,
        IShortStringHelper shortStringHelper,
        IEnumerable<IUrlSegmentProvider> urlSegmentProviders,
        ISqlContext sqlContext,
        ILogger<ContentPublishingHandler> logger)
        : INotificationHandler<ContentPublishingNotification>
    {
        private static readonly Uri _domainValidationBaseUri = new("https://localhost/");

        /// <inheritdoc/>
        public void Handle(ContentPublishingNotification notification)
        {
            var rootsToPublish = notification.PublishedEntities
                .Where(IsArticulateRoot)
                .ToList();

            if (rootsToPublish.Count == 0)
            {
                return;
            }

            try
            {
                foreach (IContent root in rootsToPublish)
                {
                    ValidateConfiguredRouteSegments(root);
                }

                ValidateRootPathMappings(rootsToPublish);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Blocked publishing due to invalid Articulate routing configuration.");
                notification.CancelOperation(new EventMessage(
                    "Articulate routing configuration",
                    ex.Message,
                    EventMessageType.Error));
            }
        }

        private void ValidateConfiguredRouteSegments(IContent root)
        {
            ArticulateRouteValidator.ValidateConfiguredRouteSegments(
                root,
                GetChildren(root.Id),
                shortStringHelper,
                urlSegmentProviders);
        }

        private void ValidateRootPathMappings(IReadOnlyList<IContent> rootsToPublish)
        {
            HashSet<int> publishingIds = [.. rootsToPublish.Select(x => x.Id)];

            var publishedRoots = contentService.GetPagedOfType(
                    rootsToPublish[0].ContentTypeId,
                    0,
                    int.MaxValue,
                    out _,
                    sqlContext.Query<IContent>().Where(x => x.Published && x.Trashed == false))
                .Where(x => !publishingIds.Contains(x.Id))
                .ToList();

            List<IContent> allRoots = [.. publishedRoots, .. rootsToPublish.Where(x => !x.Trashed)];
            if (allRoots.Count <= 1)
            {
                return;
            }

            List<Domain> domains = [.. domainCacheService.GetAll(includeWildcards: true)];
            var contentCache = new Dictionary<int, IContent>();

            IEnumerable<IGrouping<string, IContent>> groupedByPath = allRoots
                .GroupBy(root => BuildRootNodePath(root, domains, contentCache), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1);

            foreach (IGrouping<string, IContent> group in groupedByPath)
            {
                ArticulateRouteValidator.ValidateRootPathMappings(
                    group.Key,
                    group.ToList(),
                    domains,
                    _domainValidationBaseUri);
            }
        }

        private string BuildRootNodePath(IContent root, IReadOnlyList<Domain> domains, IDictionary<int, IContent> contentCache)
        {
            HashSet<int> domainContentIds = [.. ArticulateRouteValidator.DomainsForContent(root, domains).Select(x => x.ContentId)];
            List<string> segments = [];
            IContent? current = root;

            while (current is not null)
            {
                if (domainContentIds.Contains(current.Id) || current.ParentId <= 0)
                {
                    break;
                }

                string? segment = ArticulateRouteSegmentHelper.NormalizeOrNull(
                    current.GetUrlSegment(shortStringHelper, urlSegmentProviders, culture: null, published: false));

                if (segment is not null)
                {
                    segments.Add(segment);
                }

                current = GetContent(current.ParentId, contentCache);
            }

            segments.Reverse();
            return segments.Count == 0
                ? "/"
                : "/" + string.Join("/", segments) + "/";
        }

        private IContent? GetContent(int contentId, IDictionary<int, IContent> contentCache)
        {
            if (contentCache.TryGetValue(contentId, out IContent? content))
            {
                return content;
            }

            content = contentService.GetById(contentId);
            if (content is not null)
            {
                contentCache[contentId] = content;
            }

            return content;
        }

        private List<IContent> GetChildren(int rootId) =>
            contentService.EnumeratePagedChildren(
                    rootId,
                    0,
                    int.MaxValue,
                    out _)
                .ToList();

        private static bool IsArticulateRoot(IContent content) =>
            content.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.Articulate);
    }
}
