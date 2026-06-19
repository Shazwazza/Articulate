#nullable enable
using Articulate.Routing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Articulate.Components
{
    /// <summary>
    ///     Notification handler to refresh Articulate routes when the content cache is updated.
    /// </summary>
    public sealed class ContentCacheRefresherHandler(
        IUmbracoContextAccessor umbracoContextAccessor,
        IArticulateRouteRefreshState routeRefreshState,
        IPublishedContentTypeCache publishedContentTypeCache,
        IDocumentCacheService documentCacheService,
        IScopeProvider scopeProvider,
        ILogger<ContentCacheRefresherHandler> logger)
        : INotificationHandler<ContentCacheRefresherNotification>
    {
        /// <summary>
        ///     When the page/content cache is refreshed, mark Articulate routes dirty when a relevant change is detected.
        /// </summary>
        /// <remarks>
        ///     This also works for load balanced scenarios since this event executes on all servers.
        /// </remarks>
        public void Handle(ContentCacheRefresherNotification notification)
        {
            switch (notification.MessageType)
            {
                case MessageType.RefreshByPayload:
                    HandleRefreshByPayload((ContentCacheRefresher.JsonPayload[])notification.MessageObject);
                    break;
                case MessageType.RefreshById:
                case MessageType.RemoveById:
                    RefreshById(
                        (int)notification.MessageObject,
                        notification.MessageType == MessageType.RemoveById);
                    break;
                case MessageType.RefreshByInstance:
                case MessageType.RemoveByInstance:
                    HandleRefreshByInstance(notification.MessageObject);
                    break;
                case MessageType.RefreshAll:
                case MessageType.RefreshByJson:
                default:
                    break;
            }
        }

        private void HandleRefreshByPayload(ContentCacheRefresher.JsonPayload[] payloads)
        {
            foreach (ContentCacheRefresher.JsonPayload payload in payloads)
            {
                if (payload.ChangeTypes.HasTypesAny(TreeChangeTypes.Remove | TreeChangeTypes.RefreshBranch |
                                                    TreeChangeTypes.RefreshNode))
                {
                    RefreshById(
                        payload.Id,
                        payload.ChangeTypes.HasTypesAny(TreeChangeTypes.Remove));
                }
            }
        }

        private void HandleRefreshByInstance(object messageObject)
        {
            if (messageObject is not IContent content)
            {
                return;
            }

            if (!content.Published)
            {
                return;
            }

            if (ArticulateRouteChangeDetector.AffectsArticulateRoutes(
                    content.Path,
                    content.Level,
                    content.SortOrder,
                    content.ContentType.Alias,
                    GetArticulateRoots()))
            {
                MarkRoutesDirty(
                    "content cache instance refresh",
                    content.Id,
                    content.ContentType.Alias,
                    content.Path);
            }
        }

        private void RefreshById(int id, bool allowUnpublishedRefresh)
        {
            if (!umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext))
            {
                return;
            }

            using (scopeProvider.CreateScope(autoComplete: true))
            {
                IPublishedContent? item = umbracoContext.Content.GetById(id);

                // We need to handle cases where the state of siblings at a lower sort order directly affect an Articulate node's routing.
                // This will happen on copy, move, sort, unpublish, delete
                if (item is null)
                {
                    item = umbracoContext.Content.GetById(true, id);

                    // Deleted items are no longer in the published or preview cache. When the
                    // deleted item cannot be inspected, the existing routing code falls back to
                    // rebuilding Articulate routes so sibling route precedence is recalculated.
                    if (item is null)
                    {
                        MarkRoutesDirty(
                            allowUnpublishedRefresh
                                ? "content cache remove by id for unresolved content"
                                : "content cache refresh by id for unresolved content",
                            id,
                            null,
                            null);
                        return;
                    }

                    if (!allowUnpublishedRefresh)
                    {
                        return;
                    }
                }

                if (!ArticulateRouteChangeDetector.AffectsArticulateRoutes(
                        item.Path,
                        item.Level,
                        item.SortOrder,
                        item.ContentType.Alias,
                        GetArticulateRoots()))
                {
                    return;
                }

                MarkRoutesDirty(
                    allowUnpublishedRefresh ? "content cache remove by id" : "content cache refresh by id",
                    item.Id,
                    item.ContentType.Alias,
                    item.Path);
            }
        }

        private void MarkRoutesDirty(string trigger, int contentId, string? contentTypeAlias, string? contentPath)
        {
            routeRefreshState.MarkDirty();
            logger.LogInformation(
                "Marked Articulate routes dirty due to {Trigger}. ContentId: {ContentId}, ContentTypeAlias: {ContentTypeAlias}, Path: {Path}",
                trigger,
                contentId,
                contentTypeAlias,
                contentPath);
        }

        private IEnumerable<IPublishedContent> GetArticulateRoots()
        {
            IPublishedContentType articulateContentType = publishedContentTypeCache.Get(
                PublishedItemType.Content,
                ArticulateConstants.ContentType.Articulate);

            return documentCacheService.GetByContentType(articulateContentType);
        }
    }
}
