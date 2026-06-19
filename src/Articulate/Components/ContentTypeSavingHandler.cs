#nullable enable
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Articulate.Components
{
    /// <summary>
    ///     Notification handler to ensure List View is enabled for Articulate Archive and Authors content types.
    /// </summary>
    public class ContentTypeSavingHandler : INotificationHandler<ContentTypeSavingNotification>
    {
        /// <inheritdoc />
        public void Handle(ContentTypeSavingNotification notification)
        {
            foreach (IContentType c in notification.SavedEntities
                         .Where(c =>
                             c.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateArchive) ||
                             c.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateAuthors))
                         .Where(c => !c.HasIdentity))
            {
                c.ListView = Constants.DataTypes.Guids.ListViewContentGuid;
            }
        }
    }
}
