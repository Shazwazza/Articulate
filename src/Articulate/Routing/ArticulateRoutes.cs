using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using Articulate.Components;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Articulate.Routing
{
    /// <summary>
    /// Manages the MVC/Umbraco routes
    /// </summary>
    public class ArticulateRoutes
    {
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly MetaWeblogHandlerFactory _metaWeblogHandlerFactory;
        private readonly ILogger _logger;
        private readonly ContentUrls _contentUrls;

        public ArticulateRoutes(IUmbracoContextFactory umbracoContextFactory, ILogger logger, ContentUrls contentUrls, MetaWeblogHandlerFactory metaWeblogHandlerFactory)
        {
            _umbracoContextFactory = umbracoContextFactory;
            _metaWeblogHandlerFactory = metaWeblogHandlerFactory;
            _logger = logger;
            _contentUrls = contentUrls;
        }

        public void MapRoutes(RouteCollection routes)
        {
            using (var umbCtxAccessor = _umbracoContextFactory.EnsureUmbracoContext())
            {
                //find all articulate root nodes
                var contentCache = umbCtxAccessor?.UmbracoContext?.ContentCache;
                if (contentCache == null)
                {
                    throw new InvalidOperationException("Could not create Articulate routes, the content cache instance was null");
                }

                var articulateCt = contentCache.GetContentType("Articulate");
                if (articulateCt == null)
                    return;

                var articulateNodes = contentCache.GetByContentType(articulateCt).ToList();

                _logger.Info(typeof(ArticulateRoutes), "Mapping routes for {ArticulateRootNodesCount} Articulate root nodes", articulateNodes.Count);

                //NOTE: need to write lock because this might need to be remapped while the app is running if
                // any articulate nodes are updated with new values
                using (routes.GetWriteLock())
                {
                    //clear the existing articulate routes (if any)
                    RemoveExisting(routes);

                    // For each articulate root, we need to create some custom route, BUT routes can overlap
                    // based on multi-tenency so we need to deal with that. 
                    // For example a root articulate node might yield a route like:
                    //      /
                    // and another articulate root node that has a domain might have this url:
                    //      http://mydomain/
                    // but when that is processed through RoutePathFromNodeUrl, it becomes:
                    //      /
                    // which already exists and is already assigned to a specific node ID.
                    // So what we need to do in these cases is use a special route handler that takes
                    // into account the domain assigned to the route.
                    var articulateNodesGroupedByUriPath = articulateNodes
                        .GroupBy(x => RouteCollectionExtensions.RoutePathFromNodeUrl(x.Url))
                        //This is required to ensure that we create routes that are more specific first
                        // before creating routes that are less specific
                        .OrderByDescending(x => x.Key.Split('/').Length);
                    foreach (var nodeByPathGroup in articulateNodesGroupedByUriPath)
                    {
                        var nodesAsArray = nodeByPathGroup.ToArray();

                        var uriPath = nodeByPathGroup.Key;

                        MapRssRoute(routes, uriPath, nodesAsArray);
                        MapSearchRoute(routes, uriPath, nodesAsArray);
                        MapTagsAndCategoriesRoute(routes, uriPath, nodesAsArray);
                        MapMarkdownEditorRoute(routes, uriPath, nodesAsArray);
                        MapAuthorsRssRoute(routes, uriPath, nodesAsArray);

                        foreach (var articulateRootNode in nodeByPathGroup)
                        {
                            MapMetaWeblogRoute(routes, uriPath, articulateRootNode);
                            MapManifestRoute(routes, uriPath, articulateRootNode);
                            MapRsdRoute(routes, uriPath, articulateRootNode);
                            MapOpenSearchRoute(routes, uriPath, articulateRootNode);
                        }
                    }
                }
            }
        }

        private void MapMarkdownEditorRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            //var routePath = (nodeRoutePath.EnsureEndsWith('/') + "a-new/" + node.Id).TrimStart('/');
            var routeHash = nodeRoutePath.GetHashCode();

            //var name = "articulate_markdown_new" + node.Id;

            routes.MapUmbracoRoute(
                "articulate_markdown_new" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "a-new").TrimStart('/'),
                new
                {
                    controller = "MarkdownEditor",
                    action = "NewPost"
                },
                new ArticulateVirtualNodeByIdRouteHandler(_logger, _contentUrls, nodesWithPath));
        }

        private void MapRssRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            var routeHash = nodeRoutePath.GetHashCode();

            //Create the route for the /rss results
            routes.MapUmbracoRoute(
                "articulate_rss_" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "rss").TrimStart('/'),
                new
                {
                    controller = "ArticulateRss",
                    action = "Index"
                },
                new ArticulateVirtualNodeByIdRouteHandler(_logger, _contentUrls, nodesWithPath));

            routes.MapUmbracoRoute(
                "articulate_rss_xslt_" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "rss/xslt").TrimStart('/'),
                new
                {
                    controller = "ArticulateRss",
                    action = "FeedXslt"
                },
                new ArticulateVirtualNodeByIdRouteHandler(_logger, _contentUrls, nodesWithPath));
        }

        private void MapAuthorsRssRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            var routeHash = nodeRoutePath.GetHashCode();
            
            //Create the routes for the RSS author feeds
            routes.MapUmbracoRoute(
                "articulate_author_rss_" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "Author/{authorId}/rss").TrimStart('/'),
                new
                {
                    controller = "ArticulateRss",
                    action = "Author"
                },
                new ArticulateVirtualNodeByIdRouteHandler(_logger, _contentUrls, nodesWithPath));
        }

        private void MapTagsAndCategoriesRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            var routeHash = nodeRoutePath.GetHashCode();

            //Create the routes for /tags/{tag} and /categories/{category}
            routes.MapUmbracoRoute(
                "articulate_tags_" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "{action}/{tag}").TrimStart('/'),
                new
                {
                    controller = "ArticulateTags",
                    tag = UrlParameter.Optional
                },
                new ArticulateTagsRouteHandler(_logger, _contentUrls, nodesWithPath),
                //Constraints: only match either the tags or categories url names
                new { action = new TagsOrCategoryPathRouteConstraint(_contentUrls, nodesWithPath) });

            //Create the routes for the RSS specific feeds
            routes.MapUmbracoRoute(
                "articulate_tags_rss_" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "{action}/{tag}/rss").TrimStart('/'),
                new
                {
                    controller = "ArticulateRss"
                },
                new ArticulateTagsRouteHandler(_logger, _contentUrls, nodesWithPath),
                //Constraints: only match either the tags or categories url names
                new { action = new TagsOrCategoryPathRouteConstraint(_contentUrls, nodesWithPath) });
        }

        private void MapMetaWeblogRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
        {
            var routePath = (nodeRoutePath.EnsureEndsWith('/') + "metaweblog/" + node.Id).TrimStart('/');

            var name = "articulate_metaweblog_" + node.Id;
            var route = new Route(                
                routePath,
                new RouteValueDictionary(),
                new RouteValueDictionary(new { controller = new MetaWeblogRouteConstraint() }),
                _metaWeblogHandlerFactory.Create(node.Id))
                .AddRouteNameToken(name);
            
            routes.Add(name, route);
        }

        private void MapRsdRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
        {
            var routePath = (nodeRoutePath.EnsureEndsWith('/') + "rsd/" + node.Id).TrimStart('/');

            var name = "articulate_rsd_" + node.Id;
            routes.MapRoute(name,
                routePath,
                new
                {
                    controller = "Rsd",
                    action = "Index",
                    id = node.Id
                }).AddRouteNameToken(name);
        }

        private void MapOpenSearchRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
        {
            var routePath = (nodeRoutePath.EnsureEndsWith('/') + "opensearch/" + node.Id).TrimStart('/');

            var name = "articulate_opensearch_" + node.Id;
            routes.MapRoute(name,
                routePath,
                new
                {
                    controller = "OpenSearch",
                    action = "Index",
                    id = node.Id
                }).AddRouteNameToken(name);
        }

        private void MapManifestRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
        {
            var routePath = (nodeRoutePath + "wlwmanifest/" + node.Id).TrimStart('/');

            var name = "articulate_wlwmanifest_" + node.Id;
            routes.MapRoute(name,
                routePath,
                new
                {
                    controller = "WlwManifest",
                    action = "Index",
                    id = node.Id
                }).AddRouteNameToken(name);
        }

        private void MapSearchRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            //we need to group by the search url name and make unique routes amongst those,
            // alternatively we could create route constraints like we do for the tags/categories routes
            foreach (var nodeSearch in nodesWithPath.GroupBy(x => x.Value<string>("searchUrlName")))
            {
                //the hash needs to be the combination of the nodeRoutePath and the searchUrl group
                var routeHash = (nodeRoutePath + nodeSearch.Key).GetHashCode();

                //Create the route for the /search/{term} results
                routes.MapUmbracoRoute(
                    "articulate_search_" + routeHash,
                    (nodeRoutePath.EnsureEndsWith('/') + nodeSearch.Key + "/{term}").TrimStart('/'),
                    new
                    {
                        controller = "ArticulateSearch",
                        action = "Search",
                        term = UrlParameter.Optional
                    },
                    new ArticulateSearchRouteHandler(_logger, _contentUrls, nodesWithPath));
            }

            
        }

        /// <summary>
        /// Removes existing articulate custom routes
        /// </summary>
        /// <param name="routes"></param>
        private void RemoveExisting(ICollection<RouteBase> routes)
        {
            var articulateRoutes = routes
                .OfType<Route>()
                .Where(x =>
                    x.DataTokens != null
                    && x.DataTokens.ContainsKey("__RouteName")
                    && ((string) x.DataTokens["__RouteName"]).InvariantStartsWith("articulate_"))
                .ToArray();

            foreach (var route in articulateRoutes)
            {
                routes.Remove(route);
            }
        }
    }
}