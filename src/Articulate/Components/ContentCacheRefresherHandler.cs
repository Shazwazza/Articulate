using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Components
{

    public sealed class ContentCacheRefresherHandler : INotificationHandler<ContentCacheRefresherNotification>
    {
        
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly AppCaches _appCaches;

        public ContentCacheRefresherHandler(
            IUmbracoContextAccessor umbracoContextAccessor,
            AppCaches appCaches)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _appCaches = appCaches;
        }

        /// <summary>
        /// When the page/content cache is refreshed, we'll check if any articulate root nodes were included in the refresh, if so we'll set a flag
        /// on the current request to rebuild the routes at the end of the request
        /// </summary>
        /// <remarks>
        /// This will also work for load balanced scenarios since this event executes on all servers
        /// </remarks>
        public void Handle(ContentCacheRefresherNotification notification)
        {
            switch (notification.MessageType)
            {
                case MessageType.RefreshByPayload:
                    //This is the standard case for content cache refresher
                    foreach (var payload in (ContentCacheRefresher.JsonPayload[])notification.MessageObject)
                    {
                        if (payload.ChangeTypes.HasTypesAny(TreeChangeTypes.Remove | TreeChangeTypes.RefreshBranch | TreeChangeTypes.RefreshNode))
                        {
                            RefreshById(payload.Id, payload.ChangeTypes);
                        }
                    }
                    break;
                case MessageType.RefreshById:
                case MessageType.RemoveById:
                    RefreshById((int)notification.MessageObject, TreeChangeTypes.Remove);
                    break;
                case MessageType.RefreshByInstance:
                case MessageType.RemoveByInstance:
                    var content = notification.MessageObject as IContent;
                    if (content == null)
                        return;

                    if (content.ContentType.Alias.InvariantEquals(ArticulateConstants.ArticulateContentTypeAlias))
                    {
                        //ensure routes are rebuilt
                        _appCaches.RequestCache.GetCacheItem(ArticulateConstants.RefreshRoutesToken, () => true);
                    }
                    break;
            }
        }

        private void RefreshById(int id, TreeChangeTypes changeTypes)
        {
            var item = _umbracoContextAccessor?.UmbracoContext?.Content.GetById(id);

            // if it's directly related to an articulate node
            if (item != null && item.ContentType.Alias.InvariantEquals(ArticulateConstants.ArticulateContentTypeAlias))
            {
                //ensure routes are rebuilt
                _appCaches.RequestCache.GetCacheItem(ArticulateConstants.RefreshRoutesToken, () => true);
                return;
            }

            // We need to handle cases where the state of siblings at a lower sort order directly affect an Articulate node's routing.
            // This will happen on copy, move, sort, unpublish, delete
            if (item == null)
            {
                item = _umbracoContextAccessor?.UmbracoContext?.Content.GetById(true, id);

                // This will occur on delete, then what?
                // TODO: How would we know this is a node that might be at the same level/above?
                // For now we have no choice, rebuild routes on each delete :/
                if (item == null)
                {
                    _appCaches.RequestCache.GetCacheItem(ArticulateConstants.RefreshRoutesToken, () => true);
                    return;
                }
            }

            var articulateContentType = _umbracoContextAccessor?.UmbracoContext?.Content.GetContentType(ArticulateConstants.ArticulateContentTypeAlias);
            if (articulateContentType != null)
            {
                var articulateNodes = _umbracoContextAccessor?.UmbracoContext?.Content.GetByContentType(articulateContentType);
                foreach (var node in articulateNodes)
                {
                    // if the item is same level with a lower sort order it can directly affect the articulate node's route
                    if (node.Level == item.Level && node.SortOrder > item.SortOrder)
                    {
                        //ensure routes are rebuilt
                        _appCaches.RequestCache.GetCacheItem(ArticulateConstants.RefreshRoutesToken, () => true);
                        return;
                    }
                }
            }
        }
    }
}
