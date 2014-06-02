using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Articulate.Controllers;
using Articulate.Models;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Services;
using umbraco.dialogs;
using Umbraco.Web;
using Umbraco.Web.Routing;
using Umbraco.Web.UI.JavaScript;

namespace Articulate
{
    //TODO: Create these themes:
    // * https://github.com/Bartinger/phantom = DONE
    // * https://github.com/thyu/minighost
    // * http://ivanbarcia.eu/edictum.php
    // * 

    public class UmbracoEventHandler : ApplicationEventHandler
    {
        
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            //TODO: Listen to events when we need to re-map the routes when data changes!

            //map routes
            ArticulateRoutes.MapRoutes(RouteTable.Routes, UmbracoContext.Current.ContentCache);

            ContentService.Created += ContentService_Created;
            ContentService.Saving += ContentService_Saving;
            ServerVariablesParser.Parsing += ServerVariablesParser_Parsing;
        }

        void ContentService_Saving(IContentService sender, SaveEventArgs<Umbraco.Core.Models.IContent> e)
        {
            //fill in the excerpt if required
            foreach (var c in e.SavedEntities)
            {
                if (c.ContentType.Alias.InvariantEquals("ArticulateRichText")
                    || c.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                {
                    if (c.GetValue<string>("excerpt").IsNullOrWhiteSpace())
                    {
                        if (c.HasProperty("richText"))
                        {
                            var val = c.GetValue<string>("richText");
                            c.SetValue("excerpt", val == null
                                ? string.Empty 
                                : string.Join("", val.StripHtml().StripNewLines().Take(200)));
                        }
                        else
                        {
                            var val = c.GetValue<string>("markDown");
                            var md = new MarkdownDeep.Markdown();
                            val = md.Transform(val);
                            c.SetValue("excerpt", val == null
                                ? string.Empty
                                : string.Join("", val.StripHtml().StripNewLines().Take(200)));
                        }
                    }   
                }                
            }
        }

        static void ServerVariablesParser_Parsing(object sender, Dictionary<string, object> e)
        {
            if (HttpContext.Current == null) throw new InvalidOperationException("HttpContext is null");
            var urlHelper = new UrlHelper(new RequestContext(new HttpContextWrapper(HttpContext.Current), new RouteData()));
            e.Add("articulate", new Dictionary<string, object>
            {
                {"articulateImportBaseUrl", urlHelper.GetUmbracoApiServiceBaseUrl<ArticulateBlogImportController>(controller => controller.PostImportBlogMl(null))},
                {"articulatePropertyEditorsBaseUrl", urlHelper.GetUmbracoApiServiceBaseUrl<ArticulatePropertyEditorsController>(controller => controller.GetThemes())}
            });
        }

        static void ContentService_Created(IContentService sender, NewEventArgs<Umbraco.Core.Models.IContent> e)
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
