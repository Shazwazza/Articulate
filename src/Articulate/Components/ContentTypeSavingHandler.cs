using System.Linq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models;

namespace Articulate.Components
{
    public class ContentTypeSavingHandler : INotificationHandler<ContentTypeSavingNotification>
    {
        public void Handle(ContentTypeSavingNotification notification)
        {
            foreach (IContentType c in notification.SavedEntities
                .Where(c => c.Alias.InvariantEquals(ArticulateConstants.ArticulateArchiveContentTypeAlias) || c.Alias.InvariantEquals(ArticulateConstants.ArticulateAuthorsContentTypeAlias))
                .Where(c => c.HasIdentity == false))
            {
                c.IsContainer = true;
            }
        }
    }
}
