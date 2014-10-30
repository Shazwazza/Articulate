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
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Sync;
using umbraco.dialogs;
using Umbraco.Web;
using Umbraco.Web.Cache;
using Umbraco.Web.Routing;
using Umbraco.Web.UI.JavaScript;

namespace Articulate
{
    public class UmbracoEventHandler : ApplicationEventHandler
    {
        /// <summary>
        /// OVerridable method to execute when All resolvers have been initialized but resolution is not frozen so they can be modified in this method
        /// </summary>
        /// <param name="umbracoApplication"/><param name="applicationContext"/>
        protected override void ApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationStarting(umbracoApplication, applicationContext);

            UrlProviderResolver.Current.AddType<VirtualNodeUrlProvider>();
        }

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {

            //list to the init event of the application base, this allows us to bind to the actual HttpApplication events
            UmbracoApplicationBase.ApplicationInit += UmbracoApplicationBase_ApplicationInit;

            //map routes
            ArticulateRoutes.MapRoutes(RouteTable.Routes, UmbracoContext.Current.ContentCache);

            //umbraco event subscriptions
            ContentService.Created += ContentService_Created;
            ContentService.Saving += ContentService_Saving;            
            ContentService.Saved += ContentService_Saved;
            ServerVariablesParser.Parsing += ServerVariablesParser_Parsing;
            ContentTypeService.SavingContentType += ContentTypeService_SavingContentType;
            PageCacheRefresher.CacheUpdated += PageCacheRefresher_CacheUpdated;
        }

        /// <summary>
        /// Bind to the PostRequestHandlerExecute event of the HttpApplication
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void UmbracoApplicationBase_ApplicationInit(object sender, EventArgs e)
        {
            var app = (UmbracoApplicationBase) sender;
            app.PostRequestHandlerExecute += UmbracoApplication_PostRequestHandlerExecute;
        }

        /// <summary>
        /// At the end of a request, we'll check if there is a flag in the request indicating to rebuild the routes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// In some cases many articulate roots might be published at one time but we only want to rebuild the routes once so we'll do it once
        /// at the end of the request.
        /// </remarks>
        void UmbracoApplication_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            if (ApplicationContext.Current == null) return;
            if (ApplicationContext.Current.ApplicationCache.RequestCache.GetCacheItem("articulate-refresh-routes") == null) return;
            //the token was found so that means one or more articulate root nodes were changed in this request, rebuild the routes.
            ArticulateRoutes.MapRoutes(RouteTable.Routes, UmbracoContext.Current.ContentCache);
        }

        /// <summary>
        /// When the page cache is refreshed, we'll check if any articulate root nodes were included in the refresh, if so we'll set a flag
        /// on the current request to rebuild the routes at the end of the request
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// This will also work for load balanced scenarios since this event executes on all servers
        /// </remarks>
        void PageCacheRefresher_CacheUpdated(PageCacheRefresher sender, Umbraco.Core.Cache.CacheRefresherEventArgs e)
        {
            if (UmbracoContext.Current == null) return;     
            
            switch (e.MessageType)
            {                
                case MessageType.RefreshById:
                case MessageType.RemoveById:
                    var item = UmbracoContext.Current.ContentCache.GetById((int) e.MessageObject);
                    if (item != null && item.DocumentTypeAlias.InvariantEquals("Articulate"))
                    {
                        //add the unpublished entities to the request cache
                        ApplicationContext.Current.ApplicationCache.RequestCache.GetCacheItem("articulate-refresh-routes", () => true);
                    }
                    break;
                case MessageType.RefreshByInstance:
                case MessageType.RemoveByInstance:
                    var content = e.MessageObject as IContent;
                    if (content == null) return;
                    if (content.ContentType.Alias.InvariantEquals("Articulate"))
                    {
                        //add the unpublished entities to the request cache
                        UmbracoContext.Current.Application.ApplicationCache.RequestCache.GetCacheItem("articulate-refresh-routes", () => true);
                    }
                    break;                
            }
        }

        /// <summary>
        /// When a new root Articulate node is created, then create the required 2 sub nodes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ContentService_Saved(IContentService sender, SaveEventArgs<IContent> e)
        {
            foreach (var c in e.SavedEntities.Where(c => c.IsNewEntity() && c.ContentType.Alias.InvariantEquals("Articulate")))
            {
                LogHelper.Debug<UmbracoEventHandler>(() => "Creating sub nodes (authors, archive) for new Articulate node");

                //it's a root blog node, set up the required sub nodes (archive , authors)
                var articles = sender.CreateContentWithIdentity("Archive", c, "ArticulateArchive");

                LogHelper.Debug<UmbracoEventHandler>(() => "Archive node created with name: " + articles.Name);

                var authors = sender.CreateContentWithIdentity("Authors", c, "ArticulateAuthors");

                LogHelper.Debug<UmbracoEventHandler>(() => "Authors node created with name: " + authors.Name);
            }
        }

        /// <summary>
        /// Ensure list view is enabled for certain doc types when created
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ContentTypeService_SavingContentType(IContentTypeService sender, SaveEventArgs<IContentType> e)
        {
            foreach (var c in e.SavedEntities
                .Where(c => c.Alias.InvariantEquals("ArticulateArchive") || c.Alias.InvariantEquals("ArticulateAuthors"))
                .Where(c => c.IsNewEntity()))
            {
                c.IsContainer = true;
            }
        }

        void ContentService_Saving(IContentService sender, SaveEventArgs<IContent> e)
        {
            foreach (var c in e.SavedEntities
                .Where(c => c.ContentType.Alias.InvariantEquals("ArticulateRichText") || c.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                .Where(c => c.GetValue<string>("excerpt").IsNullOrWhiteSpace()))
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

        static void ContentService_Created(IContentService sender, NewEventArgs<IContent> e)
        {
            if (UmbracoContext.Current == null) return;

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
