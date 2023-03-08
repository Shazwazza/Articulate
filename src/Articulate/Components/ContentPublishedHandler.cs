using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;
using Articulate.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core;
using Microsoft.AspNetCore.Http;
using J2N.Collections.Generic;

namespace Articulate.Components
{

    public class ContentPublishedHandler : INotificationHandler<ContentPublishedNotification>
    {
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ArticulateRouter _articulateRouter;

        public ContentPublishedHandler(IUmbracoContextFactory umbracoContextFactory, IHttpContextAccessor httpContextAccessor, ArticulateRouter articulateRouter)
        {
            _umbracoContextFactory = umbracoContextFactory;
            _httpContextAccessor = httpContextAccessor;
            _articulateRouter = articulateRouter;
        }

        public void Handle(ContentPublishedNotification notification)
        {
            var e = notification;

            foreach (var c in e.PublishedEntities)
            {
                if (!c.ContentType.Alias.InvariantEquals(ArticulateConstants.ArticulateContentTypeAlias))
                    continue;

                // Using WereDirty as the content has been published/saved now
                var dirtyProps = c.GetWereDirtyProperties();
                var urlPropsToCheck = new List<string>{ "categoriesUrlName", "tagsUrlName", "searchUrlName" };
                if (dirtyProps.ContainsAny(urlPropsToCheck))
                {
                    var httpCtx = _httpContextAccessor.GetRequiredHttpContext();

                    // for each unpublished item, we want to find the url that it was previously 'published under' and store in a database table or similar
                    using (UmbracoContextReference umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext())
                    {
                        var umbCtx = umbracoContextReference.UmbracoContext;

                        // It's a root blog node thats been published
                        // Regenerate the generated routes
                        _articulateRouter.MapRoutes(httpCtx, umbCtx);
                    }
                }
            }
        }
    }
}
