using System.Linq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace Articulate.Components
{
    public sealed class SendingContentHandler : INotificationHandler<SendingContentNotification>
    {
        /// <summary>
        /// Fill in default properties when creating an Articulate root node
        /// </summary>
        public void Handle(SendingContentNotification notification)
        {
            var content = notification.Content;
            if (!content.ContentTypeAlias.InvariantEquals(ArticulateConstants.ArticulateContentTypeAlias))
                return;

            //if it's not new don't continue
            if (content.Id != default(int))
                return;

            var allProperties = content.Variants.SelectMany(x => x.Tabs.SelectMany(p => p.Properties));
            foreach (var prop in allProperties)
            {
                switch (prop.Alias)
                {
                    case "theme":
                        prop.Value = "VAPOR";
                        break;
                    case "pageSize":
                        prop.Value = 10;
                        break;
                    case "categoriesUrlName":
                        prop.Value = "categories";
                        break;
                    case "tagsUrlName":
                        prop.Value = "tags";
                        break;
                    case "searchUrlName":
                        prop.Value = "search";
                        break;
                    case "categoriesPageName":
                        prop.Value = "Categories";
                        break;
                    case "tagsPageName":
                        prop.Value = "Tags";
                        break;
                    case "searchPageName":
                        prop.Value = "Search results";
                        break;
                }
            }
        }
    }
}
