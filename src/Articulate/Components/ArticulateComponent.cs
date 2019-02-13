using Articulate.Controllers;
using Articulate.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Components;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using Umbraco.Core.Sync;
using Umbraco.Web;
using Umbraco.Web.Cache;
using Umbraco.Web.JavaScript;

namespace Articulate.Components
{
    public class ArticulateComponent : IComponent
    {
        private readonly ArticulateRoutes _articulateRoutes;
        private readonly AppCaches _appCaches;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly Configs _configs;
        private readonly ILogger _logger;

        public ArticulateComponent(ArticulateRoutes articulateRoutes, AppCaches appCaches, IUmbracoContextAccessor umbracoContextAccessor, Configs configs, ILogger logger)
        {
            _articulateRoutes = articulateRoutes;
            _appCaches = appCaches;
            _umbracoContextAccessor = umbracoContextAccessor;
            _configs = configs;
            _logger = logger;
        }

        public void Initialize()
        {
            //listen to the init event of the application base, this allows us to bind to the actual HttpApplication events
            UmbracoApplicationBase.ApplicationInit += UmbracoApplicationBase_ApplicationInit;

            //map routes
            RouteTable.Routes.MapRoute(
                "ArticulateFeeds",
                "ArticulateFeeds/{action}/{id}",
                new { controller = "Feed", action = "RenderGitHubFeed", id = 0 }
            );
            _articulateRoutes.MapRoutes(RouteTable.Routes);
            

            //umbraco event subscriptions
            ContentService.Created += ContentService_Created;
            ContentService.Saving += ContentService_Saving;
            ContentService.Saved += ContentService_Saved;
            ServerVariablesParser.Parsing += ServerVariablesParser_Parsing;
            //ContentTypeService.SavingContentType += ContentTypeService_SavingContentType; //TODO: Its internal in V8 core currently - will need newer build
            ContentCacheRefresher.CacheUpdated += ContentCacheRefresher_CacheUpdated;
            DomainCacheRefresher.CacheUpdated += DomainCacheRefresher_CacheUpdated;
            
        }        

        public void Terminate()
        {
        }

        /// <summary>
        /// Bind to the PostRequestHandlerExecute event of the HttpApplication
        /// </summary>
        private void UmbracoApplicationBase_ApplicationInit(object sender, EventArgs e)
        {
            var app = (UmbracoApplicationBase)sender;
            app.PostRequestHandlerExecute += App_PostRequestHandlerExecute;
        }

        /// <summary>
        /// At the end of a request, we'll check if there is a flag in the request indicating to rebuild the routes
        /// </summary>
        /// <remarks>
        /// In some cases many articulate roots might be published at one time but we only want to rebuild the routes once so we'll do it once
        /// at the end of the request.
        /// </remarks>
        private void App_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            if (_appCaches?.RequestCache.Get("articulate-refresh-routes") == null) return;
            //the token was found so that means one or more articulate root nodes were changed in this request, rebuild the routes.
            _articulateRoutes.MapRoutes(RouteTable.Routes);
        }

        private void ContentService_Created(IContentService sender, NewEventArgs<IContent> e)
        {
            if (UmbracoContext.Current == null) return;

            if (e.Entity.ContentType.Alias.InvariantEquals("ArticulateRichText")
                || e.Entity.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
            {
                if (_umbracoContextAccessor.UmbracoContext.Security.CurrentUser != null)
                {
                    e.Entity.SetValue("author", _umbracoContextAccessor.UmbracoContext.Security.CurrentUser.Name);
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
                e.Entity.SetValue("redirectArchive", true);
                e.Entity.SetValue("searchUrlName", "search");
                e.Entity.SetValue("categoriesPageName", "Categories");
                e.Entity.SetValue("tagsPageName", "Tags");
                e.Entity.SetValue("searchPageName", "Search results");
            }
        }

        private void ContentService_Saving(IContentService sender, SaveEventArgs<IContent> e)
        {
            if (_configs.Articulate().AutoGenerateExcerpt)
            {
                foreach (var c in e.SavedEntities
                    .Where(c => c.ContentType.Alias.InvariantEquals("ArticulateRichText") || c.ContentType.Alias.InvariantEquals("ArticulateMarkdown")))
                {
                    //fill in the excerpt if it is empty
                    if (c.GetValue<string>("excerpt").IsNullOrWhiteSpace())
                    {
                        if (c.HasProperty("richText"))
                        {
                            var val = c.GetValue<string>("richText");
                            c.SetValue("excerpt", _configs.Articulate().GenerateExcerpt(val));
                        }
                        else
                        {
                            var val = c.GetValue<IHtmlString>("markdown")?.ToString();
                            c.SetValue("excerpt", _configs.Articulate().GenerateExcerpt(val));
                        }
                    }

                    //now fill in the social description if it is empty with the excerpt
                    if (c.HasProperty("socialDescription"))
                    {
                        if (c.GetValue<string>("socialDescription").IsNullOrWhiteSpace())
                        {
                            c.SetValue("socialDescription", c.GetValue<string>("excerpt"));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// When a new root Articulate node is created, then create the required 2 sub nodes
        /// </summary>
        private void ContentService_Saved(IContentService sender, SaveEventArgs<IContent> e)
        {
            foreach (var c in e.SavedEntities.Where(c => c.HasIdentity == false && c.ContentType.Alias.InvariantEquals("Articulate")))
            {
                _logger.Debug<ArticulateComponent>("Creating sub nodes (authors, archive) for new Articulate node");

                //it's a root blog node, set up the required sub nodes (archive , authors)
                var articles = sender.CreateAndSave("Archive", c, "ArticulateArchive");

                _logger.Debug<ArticulateComponent>("Archive node created with name: {ArchiveNodeName}", articles.Name);

                var authors = sender.CreateAndSave("Authors", c, "ArticulateAuthors");

                _logger.Debug<ArticulateComponent>("Authors node created with name: {AuthorNodeName}", authors.Name);
            }
        }

        private void ServerVariablesParser_Parsing(object sender, System.Collections.Generic.Dictionary<string, object> e)
        {
            if (HttpContext.Current == null) throw new InvalidOperationException("HttpContext is null");

            if (e.ContainsKey("articulate")) return;

            var urlHelper = new UrlHelper(new RequestContext(new HttpContextWrapper(HttpContext.Current), new RouteData()));
            e["articulate"] = new Dictionary<string, object>
            {
                {"articulateImportBaseUrl", urlHelper.GetUmbracoApiServiceBaseUrl<ArticulateBlogImportController>(controller => controller.PostImportBlogMl(null))},
                {"articulateDataInstallerBaseUrl", urlHelper.GetUmbracoApiServiceBaseUrl<ArticulateBlogDataInstallController>(controller => controller.PostInstall())},
                {"articulatePropertyEditorsBaseUrl", urlHelper.GetUmbracoApiServiceBaseUrl<ArticulatePropertyEditorsController>(controller => controller.GetThemes())}
            };

            object found;
            if (e.TryGetValue("umbracoUrls", out found))
            {
                var umbUrls = (Dictionary<string, object>)found;
                umbUrls["articulateThemeEditorApiBaseUrl"] = urlHelper.GetUmbracoApiServiceBaseUrl<ThemeEditorController>(controller => controller.GetByPath(null));
            }
            else
            {
                e["umbracoUrls"] = new Dictionary<string, object>
                {
                    {"articulateThemeEditorApiBaseUrl", urlHelper.GetUmbracoApiServiceBaseUrl<ThemeEditorController>(controller => controller.GetByPath(null))}
                };
            }
        }

        private void ContentTypeService_SavingContentType(IContentTypeService sender, SaveEventArgs<IContentType> e)
        {
            foreach (var c in e.SavedEntities
                .Where(c => c.Alias.InvariantEquals("ArticulateArchive") || c.Alias.InvariantEquals("ArticulateAuthors"))
                .Where(c => c.HasIdentity == false))
            {
                c.IsContainer = true;
            }
        }

        /// <summary>
        /// When the page/content cache is refreshed, we'll check if any articulate root nodes were included in the refresh, if so we'll set a flag
        /// on the current request to rebuild the routes at the end of the request
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// This will also work for load balanced scenarios since this event executes on all servers
        /// </remarks>
        private void ContentCacheRefresher_CacheUpdated(ContentCacheRefresher sender, CacheRefresherEventArgs e)
        {
            if (UmbracoContext.Current == null) return;

            switch (e.MessageType)
            {
                case MessageType.RefreshById:
                case MessageType.RemoveById:
                    var item = _umbracoContextAccessor.UmbracoContext.ContentCache.GetById((int)e.MessageObject);
                    if (item != null && item.ContentType.Alias.InvariantEquals("Articulate"))
                    {
                        //ensure routes are rebuilt
                        _appCaches.RequestCache.GetCacheItem("articulate-refresh-routes", () => true);
                    }
                    break;

                case MessageType.RefreshByInstance:
                case MessageType.RemoveByInstance:
                    var content = e.MessageObject as IContent;
                    if (content == null) return;
                    //TODO: There is a case when there are URL conflicts with Articulate data that when the other data is unpublished
                    // we'd want to rebuild articulate routes, but not sure how to handle that and we don't want to rebuild everytime
                    // something is unpublished
                    if (content.ContentType.Alias.InvariantEquals("Articulate"))
                    {
                        //ensure routes are rebuilt
                        _appCaches.RequestCache.GetCacheItem("articulate-refresh-routes", () => true);
                    }
                    break;
            }
        }

        private void DomainCacheRefresher_CacheUpdated(DomainCacheRefresher sender, CacheRefresherEventArgs e)
        {
            //ensure routes are rebuilt
            _appCaches.RequestCache.GetCacheItem("articulate-refresh-routes", () => true);
        }

    }
}
