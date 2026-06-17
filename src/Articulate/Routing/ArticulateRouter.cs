#nullable enable
using System.Collections.Concurrent;
using Articulate.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Website.Routing;

namespace Articulate.Routing
{
    internal class ArticulateRouter
    {
        private const string MarkdownEditorControllerName = "MarkdownEditor";
        private static readonly Lock _sLocker = new();

        [ThreadStatic]
        private static RouteValueDictionary? _sSharedRouteValues;

        private static readonly string _sSearchControllerName =
            ControllerExtensions.GetControllerName<ArticulateSearchController>();

        private static readonly string _sOpenSearchControllerName =
            ControllerExtensions.GetControllerName<OpenSearchController>();

        private static readonly string _sRsdControllerName = ControllerExtensions.GetControllerName<RsdController>();

        private static readonly string _sWlwControllerName =
            ControllerExtensions.GetControllerName<WlwManifestController>();

        private static readonly string _sTagsControllerName =
            ControllerExtensions.GetControllerName<ArticulateTagsController>();

        private static readonly string _sRssControllerName =
            ControllerExtensions.GetControllerName<ArticulateRssController>();

        private static readonly string _sMetaWeblogControllerName =
            ControllerExtensions.GetControllerName<MetaWeblogController>();

        internal ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> RouteCache { get; private set; } = [];
        private readonly IControllerActionSearcher _controllerActionSearcher;
        private readonly ILogger<ArticulateRouter> _logger;
        private readonly IScopeProvider _scopeProvider;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="controllerActionSearcher"></param>
        /// <param name="scopeProvider"></param>
        /// <param name="logger"></param>
        public ArticulateRouter(
            IControllerActionSearcher controllerActionSearcher,
            IScopeProvider scopeProvider,
            ILogger<ArticulateRouter> logger)
        {
            _controllerActionSearcher = controllerActionSearcher;
            _logger = logger;
            _scopeProvider = scopeProvider;
        }

        public bool TryMatch(PathString path, RouteValueDictionary routeValues, out ArticulateRootNodeCache? articulateRootNodeCache)
        {
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = RouteCache;
            _sSharedRouteValues ??= [];
            RouteValueDictionary matchedValues = _sSharedRouteValues;

            foreach (KeyValuePair<ArticulateRouteTemplate, ArticulateRootNodeCache> item in routeCache)
            {
                matchedValues.Clear();
                if (!item.Key.Matcher.TryMatch(path, matchedValues))
                {
                    continue;
                }

                foreach (KeyValuePair<string, object?> routeValue in matchedValues)
                {
                    routeValues[routeValue.Key] = routeValue.Value;
                }

                articulateRootNodeCache = item.Value;
                return true;
            }

            articulateRootNodeCache = null;
            return false;
        }

        /// <summary>
        /// Builds all route caches.
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="umbracoContext"></param>
        /// <param name="publishedContentTypeCache"></param>
        /// <param name="documentCacheService"></param>
        public void MapRoutes(
            HttpContext httpContext,
            IUmbracoContext umbracoContext,
            IPublishedContentTypeCache publishedContentTypeCache,
            IDocumentCacheService documentCacheService)
        {
            lock (_sLocker)
            {
                using (_scopeProvider.CreateCoreScope(autoComplete: true))
                {
                    IPublishedContentType articulateCt = publishedContentTypeCache.Get(
                        PublishedItemType.Content,
                        ArticulateConstants.ContentType.Articulate);

                    var articulateNodes = documentCacheService.GetByContentType(articulateCt).ToList();

                    var domains = umbracoContext.Domains.GetAll(false).ToList();

                    var rebuiltRouteCache = new ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache>();

                    // For each articulate root, we need to create some custom route, BUT routes can overlap
                    // based on multi-tenancy so we need to deal with that.
                    // For example a root articulate node might yield a route like:
                    //      /
                    // and another articulate root node that has a domain might have this url:
                    //      http://mydomain/
                    // but when that is processed through RoutePathFromNodeUrl, it becomes:
                    //      /
                    // which already exists and is already assigned to a specific node ID.
                    // So what we need to do in these cases is use a special route handler that takes
                    // into account the domain assigned to the route.
                    IOrderedEnumerable<IGrouping<string, IPublishedContent>> articulateNodesGroupedByUriPath =
                        articulateNodes
                            .GroupBy(x => RouteCollectionExtensions.RoutePathFromNodeUrl(httpContext, x.Url()))

                            // This is required to ensure that we create routes that are more specific first
                            // before creating routes that are less specific
                            .OrderByDescending(x => x.Key.Split('/').Length);

                    foreach (IGrouping<string, IPublishedContent> nodeByPathGroup in articulateNodesGroupedByUriPath)
                    {
                        var rootNodePath = nodeByPathGroup.Key.EnsureEndsWith('/');
                        var groupedNodes = nodeByPathGroup.ToList();

                        ValidateRootPathMappings(
                            rootNodePath,
                            groupedNodes,
                            domains,
                            new Uri($"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/"));

                        if (groupedNodes.Count > 1)
                        {
                            _logger.LogDebug(
                                "Validated {Count} Articulate roots for shared path '{RootNodePath}' with domain-based disambiguation.",
                                groupedNodes.Count,
                                rootNodePath);
                        }

                        foreach (IPublishedContent articulateRootNode in groupedNodes)
                        {
                            ArticulateRouteValidator.ValidateConfiguredRouteSegments(articulateRootNode);

                            MapRssRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);

                            MapMarkdownEditorRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);
                            MapAuthorsRssRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);

                            MapSearchRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);
                            MapMetaWeblogRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);
                            MapManifestRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);
                            MapRsdRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);
                            MapOpenSearchRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);

                            // tags/cats routes are the least specific
                            MapTagsAndCategoriesRoute(rebuiltRouteCache, httpContext, rootNodePath, articulateRootNode, domains);
                        }
                    }

                    RouteCache = rebuiltRouteCache;
                }
            }
        }

        /// <summary>
        /// Generically caches a url path for a particular controller
        /// </summary>
        private void MapRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            string? controllerName,
            string? actionName,
            RouteTemplate routeTemplate,
            HttpContext httpContext,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            var art = new ArticulateRouteTemplate(routeTemplate);
            if (!routeCache.TryGetValue(art, out ArticulateRootNodeCache? dynamicRouteValues))
            {
                ControllerActionDescriptor controllerActionDescriptor =
                    _controllerActionSearcher.Find<IRenderController>(
                        httpContext,
                        controllerName,
                        actionName) ??
                    throw new InvalidOperationException("No controller found with name " + controllerName);

                dynamicRouteValues = new ArticulateRootNodeCache(controllerActionDescriptor);

                routeCache[art] = dynamicRouteValues;
            }
            else if (!string.Equals(dynamicRouteValues.ControllerActionDescriptor.ControllerName, controllerName, StringComparison.Ordinal) ||
                     !string.Equals(dynamicRouteValues.ControllerActionDescriptor.ActionName, actionName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Conflicting Articulate route template '{routeTemplate.TemplateText}' maps to both " +
                    $"'{dynamicRouteValues.ControllerActionDescriptor.ControllerName}.{dynamicRouteValues.ControllerActionDescriptor.ActionName}' and " +
                    $"'{controllerName}.{actionName}'. Check the configured route segments on your Articulate roots.");
            }

            dynamicRouteValues.Add(articulateRootNode.Id, ArticulateRouteValidator.DomainsForContent(articulateRootNode, domains));
        }

        private void ValidateRootPathMappings(
            string rootNodePath,
            IReadOnlyList<IPublishedContent> articulateRoots,
            IReadOnlyList<Domain> domains,
            Uri currentUri) =>
            ArticulateRouteValidator.ValidateRootPathMappings(rootNodePath, articulateRoots, domains, currentUri);

        private void MapOpenSearchRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            List<Domain> domains)
        {
            if (ArticulateRouteSegmentHelper.GetConfiguredSegment(articulateRootNode, "searchUrlName") is null)
            {
                return;
            }

            RouteTemplate template = TemplateParser.Parse($"{rootNodePath}opensearch/{{id}}");
            MapRoute(
                routeCache,
                _sOpenSearchControllerName,
                nameof(OpenSearchController.Index),
                template,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapRsdRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            List<Domain> domains)
        {
            RouteTemplate template = TemplateParser.Parse($"{rootNodePath}rsd/{{id}}");
            MapRoute(
                routeCache,
                _sRsdControllerName,
                nameof(RsdController.Index),
                template,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapMetaWeblogRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            List<Domain> domains)
        {
            RouteTemplate template = TemplateParser.Parse($"{rootNodePath}metaweblog/{{id}}");
            MapRoute(
                routeCache,
                _sMetaWeblogControllerName,
                nameof(MetaWeblogController.Index),
                template,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapManifestRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            List<Domain> domains)
        {
            RouteTemplate template = TemplateParser.Parse($"{rootNodePath}wlwmanifest/{{id}}");
            MapRoute(
                routeCache,
                _sWlwControllerName,
                nameof(WlwManifestController.Index),
                template,
                httpContext,
                articulateRootNode,
                domains);
        }

        /// <summary>
        /// Create route for root RSS
        /// </summary>
        /// <param name="routeCache"></param>
        /// <param name="httpContext"></param>
        /// <param name="rootNodePath"></param>
        /// <param name="articulateRootNode"></param>
        /// <param name="domains"></param>
        private void MapRssRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            RouteTemplate rssTemplate = TemplateParser.Parse($"{rootNodePath}rss");
            MapRoute(
                routeCache,
                _sRssControllerName,
                nameof(ArticulateRssController.Index),
                rssTemplate,
                httpContext,
                articulateRootNode,
                domains);

            RouteTemplate xsltTemplate = TemplateParser.Parse($"{rootNodePath}rss/xslt");
            MapRoute(
                routeCache,
                _sRssControllerName,
                nameof(ArticulateRssController.FeedXslt),
                xsltTemplate,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapAuthorsRssRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            RouteTemplate rssTemplate = TemplateParser.Parse($"{rootNodePath}author/{{authorId}}/rss");
            MapRoute(
                routeCache,
                _sRssControllerName,
                nameof(ArticulateRssController.Author),
                rssTemplate,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapMarkdownEditorRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            // Primary template derived from the routed path (handles domains)
            MapMarkdownEditorTemplates(routeCache, $"{rootNodePath}a-new", httpContext, articulateRootNode, domains);

            // Secondary template derived from the content URL (guards against cases where
            // RoutePathFromNodeUrl() collapses to '/' but the actual URL includes a segment like '/articles/').
            var contentUrl = articulateRootNode.Url();
            if (!string.IsNullOrWhiteSpace(contentUrl))
            {
                var pathOnly = Uri.TryCreate(contentUrl, UriKind.Absolute, out Uri? absolute)
                    ? absolute.AbsolutePath
                    : contentUrl;

                pathOnly = pathOnly.EnsureEndsWith('/');
                if (!pathOnly.Equals(rootNodePath, StringComparison.OrdinalIgnoreCase))
                {
                    MapMarkdownEditorTemplates(routeCache, $"{pathOnly}a-new", httpContext, articulateRootNode, domains);
                }
            }
        }

        private void MapMarkdownEditorTemplates(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            string basePath,
            HttpContext httpContext,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            // Allow both with and without trailing slash.
            RouteTemplate templateNoSlash = TemplateParser.Parse(basePath.TrimEnd('/'));
            MapRoute(
                routeCache,
                MarkdownEditorControllerName,
                "NewPost",
                templateNoSlash,
                httpContext,
                articulateRootNode,
                domains);

            RouteTemplate templateWithSlash = TemplateParser.Parse(basePath.EnsureEndsWith('/'));
            MapRoute(
                routeCache,
                MarkdownEditorControllerName,
                "NewPost",
                templateWithSlash,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapSearchRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            string? searchRoutePath = ArticulateRouteSegmentHelper.CombineRoutePath(
                rootNodePath,
                articulateRootNode.Value<string>("searchUrlName"));
            if (searchRoutePath is null)
            {
                return; // Skip route if not configured
            }

            RouteTemplate template = TemplateParser.Parse(searchRoutePath);
            MapRoute(
                routeCache,
                _sSearchControllerName,
                nameof(ArticulateSearchController.Search),
                template,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapTagsAndCategoriesRoute(
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache,
            HttpContext httpContext,
            string rootNodePath,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            foreach ((string controllerName, string actionName, string templateText) in GetTagAndCategoryRouteMappings(
                         rootNodePath,
                         articulateRootNode.Value<string>("categoriesUrlName"),
                         articulateRootNode.Value<string>("tagsUrlName")))
            {
                MapRoute(
                    routeCache,
                    controllerName,
                    actionName,
                    TemplateParser.Parse(templateText),
                    httpContext,
                    articulateRootNode,
                    domains);
            }
        }

        private static List<(string ControllerName, string ActionName, string TemplateText)> GetTagAndCategoryRouteMappings(
            string rootNodePath,
            string? categoriesUrlName,
            string? tagsUrlName)
        {
            List<(string ControllerName, string ActionName, string TemplateText)> mappings = [];

            string? categoriesRoutePath = ArticulateRouteSegmentHelper.CombineRoutePath(rootNodePath, categoriesUrlName);
            if (categoriesRoutePath is not null)
            {
                mappings.Add((_sTagsControllerName, nameof(ArticulateTagsController.Categories), $"{categoriesRoutePath}/{{tag?}}"));
                mappings.Add((_sRssControllerName, nameof(ArticulateRssController.Categories), $"{categoriesRoutePath}/{{tag}}/rss"));
            }

            string? tagsRoutePath = ArticulateRouteSegmentHelper.CombineRoutePath(rootNodePath, tagsUrlName);
            if (tagsRoutePath is null)
            {
                return mappings;
            }

            mappings.Add((_sTagsControllerName, nameof(ArticulateTagsController.Tags), $"{tagsRoutePath}/{{tag?}}"));
            mappings.Add((_sRssControllerName, nameof(ArticulateRssController.Tags), $"{tagsRoutePath}/{{tag}}/rss"));

            return mappings;
        }
    }
}
