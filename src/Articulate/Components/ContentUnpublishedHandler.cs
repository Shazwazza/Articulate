using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using System;
using System.Linq;

namespace Articulate.Components
{
    public class ContentUnpublishedHandler : INotificationHandler<ContentUnpublishedNotification>
    {
        private readonly IKeyValueService _keyValueService;

        public ContentUnpublishedHandler(IKeyValueService keyValueService)
        {
            _keyValueService = keyValueService ?? throw new ArgumentNullException(nameof(keyValueService));
        }

        public void Handle(ContentUnpublishedNotification notification)
        {
            // need to refresh the cache if an articulate node gets unpublished
            if (notification.UnpublishedEntities.Any(x => x.ContentType.Alias == "Articulate"))
            {
                _keyValueService.SetValue("Articulate.CacheRefresh", DateTime.UtcNow.ToString("o"));
            }
        }
    }
}
