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
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using umbraco.dialogs;
using Umbraco.Web;
using Umbraco.Web.Routing;
using Umbraco.Web.UI.JavaScript;

namespace Articulate
{
    public class UmbracoEventHandler : ApplicationEventHandler
    {
        
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            //TODO: Listen to events when we need to re-map the routes when data changes!

            //map routes
            ArticulateRoutes.MapRoutes(RouteTable.Routes, UmbracoContext.Current.ContentCache);

            ContentService.Created += ContentService_Created;
            ContentService.Saving += ContentService_Saving;
            ContentService.Saved += ContentService_Saved;
            ServerVariablesParser.Parsing += ServerVariablesParser_Parsing;
            ContentTypeService.SavingContentType += ContentTypeService_SavingContentType;
        }

        /// <summary>
        /// When a new root Articulate node is created, then create the required 2 sub nodes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ContentService_Saved(IContentService sender, SaveEventArgs<IContent> e)
        {
            foreach (var c in e.SavedEntities)
            {
                if (c.IsNewEntity() && c.ContentType.Alias.InvariantEquals("Articulate"))
                {
                    //it's a root blog node, set up the required sub nodes (archive , authors)
                    var articles = sender.CreateContentWithIdentity("Archive", c, "ArticulateArchive");
                    var authors = sender.CreateContentWithIdentity("Authors", c, "ArticulateAuthors");
                }
            }
        }

        /// <summary>
        /// Ensure list view is enabled for certain doc types when created
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ContentTypeService_SavingContentType(IContentTypeService sender, SaveEventArgs<Umbraco.Core.Models.IContentType> e)
        {
            foreach (var c in e.SavedEntities)
            {
                if (c.Alias.InvariantEquals("ArticulateArchive") || c.Alias.InvariantEquals("ArticulateAuthors"))
                {
                    if (c.IsNewEntity())
                    {
                        c.IsContainer = true;
                    }    
                }
                
            }
            
        }

        void ContentService_Saving(IContentService sender, SaveEventArgs<Umbraco.Core.Models.IContent> e)
        {
            
            foreach (var c in e.SavedEntities)
            {
                //fill in the excerpt if required
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
                            var val = c.GetValue<string>("markdown");
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
                    if (UmbracoContext.Current.Security.CurrentUser != null)
                    {
                        e.Entity.SetValue("author", UmbracoContext.Current.Security.CurrentUser.Name);    
                    }
                    e.Entity.SetValue("publishedDate", DateTime.Now);
                    e.Entity.SetValue("enableComments", 1);
                }
                else if (e.Entity.ContentType.Alias.InvariantEquals("Articulate"))
                {
                    e.Entity.SetValue("theme", "VAPOR");
                    e.Entity.SetValue("pageSize", 10);
                    e.Entity.SetValue("categoriesUrlName", "categories");
                    e.Entity.SetValue("tagsUrlName", "tags");
                    e.Entity.SetValue("searchUrlName", "search");
                    e.Entity.SetValue("categoriesPageName", "Categories");
                    e.Entity.SetValue("tagsPageName", "Tags");
                    e.Entity.SetValue("searchPageName", "Search results");
                }
            }
        }
    }
}
