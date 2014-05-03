using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Routing;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class UmbracoEventHandler : ApplicationEventHandler
    {
        protected override void ApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ContentFinderResolver.Current
                .InsertType<TagContentFinder>();
        }

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ContentService.Created += ContentService_Created;
        }

        static void ContentService_Created(IContentService sender, Umbraco.Core.Events.NewEventArgs<Umbraco.Core.Models.IContent> e)
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
