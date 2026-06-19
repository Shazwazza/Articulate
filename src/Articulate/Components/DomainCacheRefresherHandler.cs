#nullable enable
using Articulate.Routing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Articulate.Components
{
    /// <summary>
    ///     Notification handler to refresh Articulate routes when domains are updated in the cache.
    /// </summary>
    public sealed class DomainCacheRefresherHandler(
        IArticulateRouteRefreshState routeRefreshState,
        ILogger<DomainCacheRefresherHandler> logger)
        : INotificationHandler<DomainCacheRefresherNotification>
    {
        /// <inheritdoc />
        public void Handle(DomainCacheRefresherNotification notification)
        {
            routeRefreshState.MarkDirty();
            logger.LogInformation("Marked Articulate routes dirty due to domain cache refresh.");
        }
    }
}
