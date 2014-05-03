using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Routing;
using Articulate.Models;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class TagContentFinder : ContentFinderByNiceUrl
    {
        public override bool TryFindContent(PublishedContentRequest contentRequest)
        {
            var route = !contentRequest.HasDomain ? contentRequest.Uri.GetAbsolutePathDecoded() : contentRequest.Domain.RootNodeId.ToString() + DomainHelper.PathRelativeToDomain(contentRequest.DomainUri, contentRequest.Uri.GetAbsolutePathDecoded());

            var parts = route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {   
                //if it ends with '/tags'
                if (parts[parts.Length - 1].InvariantEquals("tags"))
                {
                    route = route.Substring(0, route.LastIndexOf("/tags", StringComparison.InvariantCultureIgnoreCase) + 1);
                    var parent = FindContent(contentRequest, route);
                    if (parent != null && parent.DocumentTypeAlias.InvariantEquals("Articulate"))
                    {
                        //ok, so we want to render a tag page
                        contentRequest.PublishedContent = new TagPage();
                        return true;
                    }
                }    
                //if the 2nd last item is /tags
                if (parts.Length > 1 && parts[parts.Length - 2].InvariantEquals("tags"))
                {
                    
                }
            }
            
            
            var byNiceUrl = new ContentFinderByNiceUrl();

            return false;
        }
    }

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
