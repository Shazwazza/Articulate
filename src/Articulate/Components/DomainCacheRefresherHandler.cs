using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace Articulate.Components
{

    public sealed class DomainCacheRefresherHandler : INotificationHandler<DomainCacheRefresherNotification>
    {
        private readonly AppCaches _appCaches;

        public DomainCacheRefresherHandler(AppCaches appCaches) => _appCaches = appCaches;

        public void Handle(DomainCacheRefresherNotification notification)
        {
            //ensure routes are rebuilt
            _appCaches.RequestCache.GetCacheItem(ArticulateConstants.RefreshRoutesToken, () => true);
        }
    }
}
