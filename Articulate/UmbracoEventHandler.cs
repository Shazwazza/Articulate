using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Articulate.Controllers;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;
using Umbraco.Web.UI.JavaScript;

namespace Articulate
{
    //TODO: Create these themes:
    // * https://github.com/Bartinger/phantom
    // * https://github.com/thyu/minighost
    // * http://ivanbarcia.eu/edictum.php
    // * 

    public class UmbracoEventHandler : ApplicationEventHandler
    {
        protected override void ApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ContentFinderResolver.Current.InsertType<ArticulateContentFinder>();
        }

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ContentService.Created += ContentService_Created;
            ServerVariablesParser.Parsing += ServerVariablesParser_Parsing;
        }

        static void ServerVariablesParser_Parsing(object sender, Dictionary<string, object> e)
        {
            if (HttpContext.Current == null) throw new InvalidOperationException("HttpContext is null");
            var urlHelper = new UrlHelper(new RequestContext(new HttpContextWrapper(HttpContext.Current), new RouteData()));            
            e.Add("articulate", new Dictionary<string, object>
            {
                {"articulateImportBaseUrl", urlHelper.GetUmbracoApiServiceBaseUrl<ArticulateBlogImportController>(controller => controller.ImportBlogMl(null))},
            });
        }

        static void ContentService_Created(IContentService sender, Umbraco.Core.Events.NewEventArgs<Umbraco.Core.Models.IContent> e)
        {
            if (UmbracoContext.Current != null)
            {
                if (e.Entity.ContentType.Alias.InvariantEquals("ArticulateRichText")
                    || e.Entity.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                {
                    e.Entity.SetValue("author", UmbracoContext.Current.Security.CurrentUser.Name);
                    e.Entity.SetValue("publishedDate", DateTime.Now);                    
                }
                else if (e.Entity.ContentType.Alias.InvariantEquals("Articulate"))
                {
                    e.Entity.SetValue("pageSize", 10);
                    e.Entity.SetValue("categoriesUrlName", "categories");
                    e.Entity.SetValue("tagsUrlName", "tags");
                    e.Entity.SetValue("searchUrlName", "tags");
                    e.Entity.SetValue("categoriesPageName", "Categories");
                    e.Entity.SetValue("tagsPageName", "Tags");
                    e.Entity.SetValue("searchPageName", "Search results");
                }
            }
        }
    }
}
