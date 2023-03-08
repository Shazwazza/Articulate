using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;
using Articulate.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core;
using Microsoft.AspNetCore.Http;
using J2N.Collections.Generic;
using Umbraco.Cms.Core.Services;
using System;

namespace Articulate.Components
{
    public class ContentPublishedHandler : INotificationHandler<ContentPublishedNotification>
    {
        private readonly IKeyValueService _keyValueService;
        private readonly CacheRefreshKey _appKey;

        public ContentPublishedHandler(IKeyValueService keyValueService, CacheRefreshKey appKey)
        {
            _keyValueService = keyValueService ?? throw new ArgumentNullException(nameof(keyValueService));
            _appKey = appKey ?? throw new ArgumentNullException(nameof(appKey));
        }

        public void Handle(ContentPublishedNotification notification)
        {
            var e = notification;

            foreach (var c in e.PublishedEntities)
            {
                if (!c.ContentType.Alias.InvariantEquals(ArticulateConstants.ArticulateContentTypeAlias))
                {
                    continue;
                }

                // TODO: Need to refresh cache if an Articulate node gets published after being unpublished

                // Using WereDirty as the content has been published/saved now
                var dirtyProps = c.GetWereDirtyProperties();
                var urlPropsToCheck = new List<string>{ "categoriesUrlName", "tagsUrlName", "searchUrlName", "Published" };
                if (dirtyProps.ContainsAny(urlPropsToCheck))
                {
                    _keyValueService.SetValue(_appKey.Key, DateTime.UtcNow.ToString("o"));
                }
            }
        }
    }
}
