using Articulate.Controllers;
using Articulate.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Articulate.Routing;
using HeyRed.MarkdownSharp;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Changes;
using Umbraco.Core.Services.Implement;
using Umbraco.Core.Sync;
using Umbraco.Web;
using Umbraco.Web.Cache;
using Umbraco.Web.Editors;
using Umbraco.Web.JavaScript;
using IComponent = Umbraco.Core.Composing.IComponent;

namespace Articulate.Components
{
    public class ArticulateComponent : IComponent
    {
        private const string RefreshRoutesToken = "articulate-refresh-routes";
        private const string ArticulateContentTypeAlias = "articulate";
        private readonly ArticulateRoutes _articulateRoutes;
        private readonly AppCaches _appCaches;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly Configs _configs;
        private readonly ILogger _logger;
        private readonly IContentTypeService _contentTypeService;
        private readonly ILocalizationService _languageService;

        [Obsolete("Use ctor specifying all overloads")]
        public ArticulateComponent(ArticulateRoutes articulateRoutes, AppCaches appCaches, IUmbracoContextAccessor umbracoContextAccessor, Configs configs, ILogger logger)
            : this(articulateRoutes, appCaches, umbracoContextAccessor, configs, logger, Current.Services.ContentTypeService, Current.Services.LocalizationService) { }

        public ArticulateComponent(ArticulateRoutes articulateRoutes, AppCaches appCaches, IUmbracoContextAccessor umbracoContextAccessor, Configs configs, ILogger logger, IContentTypeService contentTypeService, ILocalizationService languageService)
        {
            _articulateRoutes = articulateRoutes;
            _appCaches = appCaches;
            _umbracoContextAccessor = umbracoContextAccessor;
            _configs = configs;
            _logger = logger;
            _contentTypeService = contentTypeService;
            _languageService = languageService;
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
            if (!content.ContentTypeAlias.InvariantEquals(ArticulateContentTypeAlias)) return;

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
            if (_appCaches?.RequestCache.Get(RefreshRoutesToken) == null) return;
            //the token was found so that means one or more articulate root nodes were changed in this request, rebuild the routes.
            _articulateRoutes.MapRoutes(RouteTable.Routes);
        }

        /// <summary>
        /// Used to set a content item property values while taking into account if it's variant or invariant
        /// </summary>
        /// <param name="content"></param>
        /// <param name="propertyAlias"></param>
        /// <param name="propertyValue"></param>
        /// <param name="onlyIfNotSet"></param>
        private void SetPropertyValue<T>(IContent content, string propertyAlias, Func<IContentType, ContentCultureInfos, T> propertyValueGetter, bool onlyIfNotSet = false)
        {
            var contentType = _contentTypeService.Get(content.ContentType.Id);
            if (contentType == null)
                throw new InvalidOperationException($"No content type found by id {content.ContentType.Id}");

            if (content.ContentType.VariesByCulture())
            {
                foreach(var c in content.CultureInfos)
                {
                    var propertyType = contentType.CompositionPropertyTypes.FirstOrDefault(x => x.Alias == propertyAlias);
                    if (propertyType == null)
                        throw new InvalidOperationException($"No property type found by alias {propertyAlias}");

                    var propertyValue = propertyValueGetter(contentType, c);
                    if (propertyValue == null || (propertyValue is string propValAsString && string.IsNullOrWhiteSpace(propValAsString)))
                        continue;

                    if (onlyIfNotSet && (!content.GetValue<T>(propertyAlias, propertyType.VariesByCulture() ? c.Culture : null)?.Equals(default(T)) ?? false))
                        continue;

                    content.SetValue(propertyAlias, propertyValue, propertyType.VariesByCulture() ? c.Culture : null);
                }
            }
            else
            {
                var propertyValue = propertyValueGetter(contentType, null);
                if (propertyValue == null || (propertyValue is string propValAsString && string.IsNullOrWhiteSpace(propValAsString)))
                    return;

                if (onlyIfNotSet && (!content.GetValue<T>(propertyAlias)?.Equals(default(T)) ?? false))
                    return;

                content.SetValue(propertyAlias, propertyValue);
            }
            
        }

        private void ContentService_Saving(IContentService sender, SaveEventArgs<IContent> e)
        {
            foreach (var content in e.SavedEntities)
            {

                if (content.ContentType.Alias.InvariantEquals("ArticulateRichText")
                    || content.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                {
                    SetPropertyValue(content, "publishedDate", (contentType, culture) => DateTime.Now, true);

                    SetPropertyValue(content, "author", (contentType, culture) => _umbracoContextAccessor?.UmbracoContext?.Security?.CurrentUser?.Name, true);

                    if (!content.HasIdentity)
                    {
                        //default values
                        SetPropertyValue(content, "enableComments", (contentType, culture) => 1);
                    }
                }

                if (_configs.Articulate().AutoGenerateExcerpt)
                {
                    if (content.ContentType.Alias.InvariantEquals("ArticulateRichText")
                        || content.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                    {

                        //fill in the excerpt if it is empty
                        SetPropertyValue(content, "excerpt",
                            (contentType, culture) =>
                            {
                                if (content.HasProperty("richText"))
                                {
                                    var richTextProperty = contentType.CompositionPropertyTypes.First(x => x.Alias == "richText");
                                    var val = content.GetValue<string>("richText", richTextProperty.VariesByCulture() ? culture?.Culture : null);
                                    return _configs.Articulate().GenerateExcerpt(val);
                                }
                                else
                                {
                                    var markdownProperty = contentType.CompositionPropertyTypes.First(x => x.Alias == "markdown");
                                    var val = content.GetValue<string>("markdown", markdownProperty.VariesByCulture() ? culture?.Culture : null);
                                    var md = new Markdown();
                                    var html = md.Transform(val);
                                    return _configs.Articulate().GenerateExcerpt(html);
                                }
                            }, true);

                        //now fill in the social description if it is empty with the excerpt
                        if (content.HasProperty("socialDescription"))
                        {
                            SetPropertyValue(content, "socialDescription", (contentType, culture) =>
                            {
                                var excerptProperty = contentType.CompositionPropertyTypes.First(x => x.Alias == "excerpt");
                                return content.GetValue<string>("excerpt", excerptProperty.VariesByCulture() ? culture?.Culture : null);
                            }, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// When a new root Articulate node is created, then create the required 2 sub nodes
        /// </summary>
        private void ContentService_Saved(IContentService contentService, SaveEventArgs<IContent> e)
        {
            foreach (var c in e.SavedEntities)
            {
                if (!c.WasPropertyDirty("Id") || !c.ContentType.Alias.InvariantEquals(ArticulateContentTypeAlias)) continue;

                //it's a root blog node, set up the required sub nodes (archive , authors) if they don't exist

                var defaultLang = _languageService.GetDefaultLanguageIsoCode();

                var children = contentService.GetPagedChildren(c.Id, 0, 10, out var total).ToList();
                if (total == 0 || children.All(x => x.ContentType.Alias != "ArticulateArchive"))
                {
                    var archiveContentType = _contentTypeService.Get("ArticulateArchive");
                    if (archiveContentType != null)
                    {
                        if (archiveContentType.VariesByCulture())
                        {
                            var articles = contentService.Create("", c, "ArticulateArchive");
                            articles.SetCultureName("Archive", defaultLang);
                            contentService.Save(articles);
                        }
                        else
                        {
                            var articles = contentService.CreateAndSave("Archive", c, "ArticulateArchive");
                        }
                    }
                }

                if (total == 0 || children.All(x => x.ContentType.Alias != "ArticulateAuthors"))
                {
                    var authorContentType = _contentTypeService.Get("ArticulateAuthors");
                    if (authorContentType != null)
                    {
                        if (authorContentType.VariesByCulture())
                        {
                            var authors = contentService.Create("", c, "ArticulateAuthors");
                            authors.SetCultureName("Authors", defaultLang);
                            contentService.Save(authors);
                        }
                        else
                        {
                            var authors = contentService.CreateAndSave("Authors", c, "ArticulateAuthors");
                        }
                    }                    
                }
            }
        }

        private void ServerVariablesParser_Parsing(object sender, System.Collections.Generic.Dictionary<string, object> e)
        {
            if (HttpContext.Current == null) throw new InvalidOperationException("HttpContext is null");

            if (e.ContainsKey(ArticulateContentTypeAlias)) return;

            var urlHelper = new UrlHelper(new RequestContext(new HttpContextWrapper(HttpContext.Current), new RouteData()));
            e[ArticulateContentTypeAlias] = new Dictionary<string, object>
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
                case MessageType.RefreshByPayload:
                    //This is the standard case for content cache refresher
                    foreach (var payload in (ContentCacheRefresher.JsonPayload[])e.MessageObject)
                    {
                        if (payload.ChangeTypes.HasTypesAny(TreeChangeTypes.Remove | TreeChangeTypes.RefreshBranch | TreeChangeTypes.RefreshNode))
                        {
                            RefreshById(payload.Id, payload.ChangeTypes);
                        }
                    }
                    break;
                case MessageType.RefreshById:
                case MessageType.RemoveById:
                    RefreshById((int)e.MessageObject, TreeChangeTypes.Remove);
                    break;
                case MessageType.RefreshByInstance:
                case MessageType.RemoveByInstance:
                    var content = e.MessageObject as IContent;
                    if (content == null) return;
                    
                    if (content.ContentType.Alias.InvariantEquals(ArticulateContentTypeAlias))
                    {
                        //ensure routes are rebuilt
                        _appCaches.RequestCache.GetCacheItem(RefreshRoutesToken, () => true);
                    }
                    break;
            }
        }

        private void RefreshById(int id, TreeChangeTypes changeTypes)
        {
            var item = _umbracoContextAccessor?.UmbracoContext?.Content.GetById(id);

            // if it's directly related to an articulate node
            if (item != null && item.ContentType.Alias.InvariantEquals(ArticulateContentTypeAlias))
            {
                //ensure routes are rebuilt
                _appCaches.RequestCache.GetCacheItem(RefreshRoutesToken, () => true);
                return;
            }

            // We need to handle cases where the state of siblings at a lower sort order directly affect an Articulate node's routing.
            // This will happen on copy, move, sort, unpublish, delete
            if (item == null)
            {
                item = _umbracoContextAccessor?.UmbracoContext?.Content.GetById(true, id);

                // This will occur on delete, then what?
                // TODO: How would we know this is a node that might be at the same level/above?
                // For now we have no choice, rebuild routes on each delete :/
                if (item == null)
                {
                    _appCaches.RequestCache.GetCacheItem(RefreshRoutesToken, () => true);
                    return;
                }
            }

            var articulateContentType = _umbracoContextAccessor?.UmbracoContext?.Content.GetContentType(ArticulateContentTypeAlias);
            if (articulateContentType != null)
            {
                var articulateNodes = _umbracoContextAccessor?.UmbracoContext?.Content.GetByContentType(articulateContentType);
                foreach (var node in articulateNodes)
                {
                    // if the item is same level with a lower sort order it can directly affect the articulate node's route
                    if (node.Level == item.Level && node.SortOrder > item.SortOrder)
                    {
                        //ensure routes are rebuilt
                        _appCaches.RequestCache.GetCacheItem(RefreshRoutesToken, () => true);
                        return;
                    }
                }
            }
        }

        private void DomainCacheRefresher_CacheUpdated(DomainCacheRefresher sender, CacheRefresherEventArgs e)
        {
            //ensure routes are rebuilt
            _appCaches.RequestCache.GetCacheItem(RefreshRoutesToken, () => true);
        }

    }
}
