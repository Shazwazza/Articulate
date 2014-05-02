using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Web;

namespace Articulate
{
    public class UmbracoEventHandler : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ContentService.Created += ContentService_Created;
        }

        void ContentService_Created(IContentService sender, Umbraco.Core.Events.NewEventArgs<Umbraco.Core.Models.IContent> e)
        {
            if (UmbracoContext.Current != null)
            {
                if (e.Entity.ContentType.Alias.InvariantEquals("ArticulateRichText")
                    || e.Entity.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                {
                    e.Entity.SetValue("author", UmbracoContext.Current.UmbracoUser.Name);
                    e.Entity.SetValue("publishedDate", DateTime.Now);
                }    
            }
        }
    }
}
