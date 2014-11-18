using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;

namespace Articulate
{
    /// <summary>
    /// Manages the MVC/Umbraco routes
    /// </summary>
    public static class ArticulateRoutes
    {
        public static void MapRoutes(RouteCollection routes, ContextualPublishedCache umbracoCache)
        {
            //find all articulate root nodes
            var articulateNodes = umbracoCache.GetByXPath("//Articulate").ToArray();


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
                var groups = articulateNodes.GroupBy(x => RouteCollectionExtensions.RoutePathFromNodeUrl(x.Url));
                foreach (var grouping in groups)
                {
                    var nodesAsArray = grouping.ToArray();

                    MapRssRoute(routes, grouping.Key, nodesAsArray);
                    MapSearchRoute(routes, grouping.Key, nodesAsArray);                                        
                    MapTagsAndCategoriesRoute(routes, grouping.Key, nodesAsArray);

                    foreach (var content in grouping)
                    {
                        MapMetaWeblogRoute(routes, grouping.Key, content);
                        MapManifestRoute(routes, grouping.Key, content);
                        MapRsdRoute(routes, grouping.Key, content);
                        MapMarkdownEditorRoute(routes, grouping.Key, content);    
                    }

                }
            }
        }

        private static void MapMarkdownEditorRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
        {
            var routePath = (nodeRoutePath.EnsureEndsWith('/') + "a-new/" + node.Id).TrimStart('/');

            var name = "articulate_markdown_new" + node.Id;
            routes.MapRoute(name,
                routePath,
                new
                {
                    controller = "MarkdownEditor",
                    action = "NewPost",
                    id = node.Id
                }).AddRouteNameToken(name);
        }

        private static void MapRssRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
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
                new UmbracoVirtualNodeByIdRouteHandler(nodesWithPath));

            routes.MapUmbracoRoute(
                "articulate_rss_xslt_" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "rss/xslt").TrimStart('/'),
                new
                {
                    controller = "ArticulateRss",
                    action = "FeedXslt"
                },
                new UmbracoVirtualNodeByIdRouteHandler(nodesWithPath));
        }

        private static void MapTagsAndCategoriesRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
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
                new ArticulateTagsRouteHandler(nodesWithPath),
                //Constraints: only match either the tags or categories url names
                new { action = new TagsOrCategoryPathRouteConstraint(nodesWithPath) });

            //Create the routes for the RSS specific feeds
            routes.MapUmbracoRoute(
                "articulate_tags_rss_" + routeHash,
                (nodeRoutePath.EnsureEndsWith('/') + "{action}/{tag}/rss").TrimStart('/'),
                new
                {
                    controller = "ArticulateRss"
                },
                new ArticulateTagsRouteHandler(nodesWithPath),
                //Constraints: only match either the tags or categories url names
                new { action = new TagsOrCategoryPathRouteConstraint(nodesWithPath) });
        }

        private static void MapMetaWeblogRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
        {
            var routePath = (nodeRoutePath.EnsureEndsWith('/') + "metaweblog/" + node.Id).TrimStart('/');

            var name = "articulate_metaweblog_" + node.Id;
            var route = new Route(                
                routePath,
                new RouteValueDictionary(),
                new RouteValueDictionary(new { controller = new MetaWeblogRouteConstraint() }),
                new MetaWeblogHandler(node.Id))
                .AddRouteNameToken(name);
            
            routes.Add(name, route);
        }

        private static void MapRsdRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
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

        private static void MapManifestRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent node)
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

        private static void MapSearchRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            //we need to group by the search url name and make unique routes amongst those,
            // alternatively we could create route constraints like we do for the tags/categories routes
            foreach (var nodeSearch in nodesWithPath.GroupBy(x => x.GetPropertyValue<string>("searchUrlName")))
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
                    new ArticulateSearchRouteHandler(nodesWithPath));
            }

            
        }

        /// <summary>
        /// Removes existing articulate custom routes
        /// </summary>
        /// <param name="routes"></param>
        private static void RemoveExisting(ICollection<RouteBase> routes)
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