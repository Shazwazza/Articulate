using Articulate.Controllers;
using Articulate.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Cache;
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
using Umbraco.Web.Editors;
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

            //umbraco event subscriptions
            ContentService.Saving += ContentService_Saving;
            ContentService.Saved += ContentService_Saved;
            ServerVariablesParser.Parsing += ServerVariablesParser_Parsing;
            ContentTypeService.Saving += ContentTypeService_SavingContentType;
            ContentCacheRefresher.CacheUpdated += ContentCacheRefresher_CacheUpdated;
            DomainCacheRefresher.CacheUpdated += DomainCacheRefresher_CacheUpdated;
            EditorModelEventManager.SendingContentModel += EditorModelEventManager_SendingContentModel;

            InitializeRoutes(_articulateRoutes, RouteTable.Routes);
        }

        /// <summary>
        /// Fill in default properties when creating an Articulate root node
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditorModelEventManager_SendingContentModel(System.Web.Http.Filters.HttpActionExecutedContext sender, EditorModelEventArgs<Umbraco.Web.Models.ContentEditing.ContentItemDisplay> e)
        {
            var content = e.Model;
            if (!content.ContentTypeAlias.InvariantEquals("Articulate")) return;

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

        public void Terminate()
        {
        }

        /// <summary>
        /// Bind to the PostRequestHandlerExecute event of the HttpApplication
        /// </summary>
        private void UmbracoApplicationBase_ApplicationInit(object sender, EventArgs e)
        {
            var app = (UmbracoApplicationBase)sender;
            //app.ResolveRequestCache += App_ResolveRequestCache;
            app.PostRequestHandlerExecute += App_PostRequestHandlerExecute;
        }

        private static void InitializeRoutes(ArticulateRoutes articulateRoutes, RouteCollection routes)
        {
            //map routes
            routes.MapRoute(
                "ArticulateFeeds",
                "ArticulateFeeds/{action}/{id}",
                new { controller = "Feed", action = "RenderGitHubFeed", id = 0 }
            );
            articulateRoutes.MapRoutes(routes);
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
        
        private void ContentService_Saving(IContentService sender, SaveEventArgs<IContent> e)
        {
            foreach (var content in e.SavedEntities)
            {
                if (!content.HasIdentity)
                {
                    if (content.ContentType.Alias.InvariantEquals("ArticulateRichText")
                        || content.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                    {
                        if (_umbracoContextAccessor?.UmbracoContext?.Security?.CurrentUser != null)
                        {
                            content.SetValue("author", _umbracoContextAccessor.UmbracoContext.Security.CurrentUser.Name);
                        }
                        content.SetValue("publishedDate", DateTime.Now);
                        content.SetValue("enableComments", 1);
                    }
                }

                if (_configs.Articulate().AutoGenerateExcerpt)
                {
                    if (content.ContentType.Alias.InvariantEquals("ArticulateRichText") || content.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                    {
                        //fill in the excerpt if it is empty
                        if (content.GetValue<string>("excerpt").IsNullOrWhiteSpace())
                        {
                            if (content.HasProperty("richText"))
                            {
                                var val = content.GetValue<string>("richText");
                                content.SetValue("excerpt", _configs.Articulate().GenerateExcerpt(val));
                            }
                            else
                            {
                                var val = content.GetValue<IHtmlString>("markdown")?.ToString();
                                content.SetValue("excerpt", _configs.Articulate().GenerateExcerpt(val));
                            }
                        }

                        //now fill in the social description if it is empty with the excerpt
                        if (content.HasProperty("socialDescription"))
                        {
                            if (content.GetValue<string>("socialDescription").IsNullOrWhiteSpace())
                            {
                                content.SetValue("socialDescription", content.GetValue<string>("excerpt"));
                            }
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
            foreach (var c in e.SavedEntities)
            {
                if (!c.WasPropertyDirty("Id") || !c.ContentType.Alias.InvariantEquals("Articulate")) continue;
                
                //it's a root blog node, set up the required sub nodes (archive , authors) if they don't exist

                var children = sender.GetPagedChildren(c.Id, 0, 10, out var total).ToList();
                if (total == 0 || children.All(x => x.ContentType.Alias != "ArticulateArchive"))
                {
                    var articles = sender.CreateAndSave("Archive", c, "ArticulateArchive");
                }

                if (total == 0 || children.All(x => x.ContentType.Alias != "ArticulateArchive"))
                {
                    var authors = sender.CreateAndSave("Authors", c, "ArticulateAuthors");
                }
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
            switch (e.MessageType)
            {
                case MessageType.RefreshById:
                case MessageType.RemoveById:
                    var item = _umbracoContextAccessor?.UmbracoContext?.ContentCache.GetById((int)e.MessageObject);
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
