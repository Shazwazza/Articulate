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

        public ContentPublishedHandler(IKeyValueService keyValueService)
        {
            _keyValueService = keyValueService ?? throw new System.ArgumentNullException(nameof(keyValueService));
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
                var urlPropsToCheck = new List<string>{ "categoriesUrlName", "tagsUrlName", "searchUrlName" };
                if (dirtyProps.ContainsAny(urlPropsToCheck))
                {
                    _keyValueService.SetValue("Articulate.CacheRefresh", DateTime.UtcNow.ToString("o"));
                }
            }
        }
    }
}
