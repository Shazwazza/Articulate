using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using System;
using System.Linq;
using Articulate.Routing;

namespace Articulate.Components
{
    public class ContentUnpublishedHandler : INotificationHandler<ContentUnpublishedNotification>
    {
        private readonly IKeyValueService _keyValueService;
        private readonly CacheRefreshKey _appKey;

        public ContentUnpublishedHandler(IKeyValueService keyValueService, CacheRefreshKey appKey)
        {
            _keyValueService = keyValueService ?? throw new ArgumentNullException(nameof(keyValueService));
            _appKey = appKey ?? throw new ArgumentNullException(nameof(appKey));
        }

        public void Handle(ContentUnpublishedNotification notification)
        {
            // need to refresh the cache if an articulate node gets unpublished
            if (notification.UnpublishedEntities.Any(x => x.ContentType.Alias == "Articulate"))
            {
                _keyValueService.SetValue(_appKey.Key, DateTime.UtcNow.ToString("o"));
            }
        }
    }
}
