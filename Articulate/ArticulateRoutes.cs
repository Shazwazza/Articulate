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
                    var groupHash = grouping.Key.GetHashCode();

                    RemoveExisting(routes,
                        "articulate_rss_" + groupHash,
                        "articulate_rss_xslt_" + groupHash,
                        "articulate_tags_" + groupHash,
                        "articulate_tags_rss_" + groupHash,
                        "articulate_search_" + groupHash,
                        "articulate_metaweblog_" + groupHash,
                        "articulate_rsd_" + groupHash,
                        "articulate_wlwmanifest_" + groupHash,
                        "articulate_markdown_" + groupHash);

                    var nodesAsArray = grouping.ToArray();

                    MapRssRoute(routes, grouping.Key, nodesAsArray);
                    MapSearchRoute(routes, grouping.Key, nodesAsArray);
                    MapManifestRoute(routes, grouping.Key, nodesAsArray);
                    MapRsdRoute(routes, grouping.Key, nodesAsArray);
                    MapMetaWeblogRoute(routes, grouping.Key);
                    MapTagsAndCategoriesRoute(routes, grouping.Key, nodesAsArray);
                    MapMarkdownEditorRoute(routes, grouping.Key, nodesAsArray);

                    foreach (var node in grouping)
                    {
                        
                    }

                    
                }
            }
        }

        private static void MapMarkdownEditorRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            var routeHash = nodeRoutePath.GetHashCode();

            var routePath = RouteCollectionExtensions.RoutePathFromNodeUrl(nodeRoutePath.EnsureEndsWith('/') + "a-new/{id}").TrimStart('/');

            //TODO: FIx this!

            //routes.MapRoute("articulate_markdown_new" + routeHash, routePath,
            //            new
            //            {
            //                controller = "MarkdownEditor",
            //                action = "NewPost",
            //                id = node.Id
            //            });
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

        private static void MapMetaWeblogRoute(RouteCollection routes, string nodeRoutePath)
        {
            var routeHash = nodeRoutePath.GetHashCode();

            var routePath = (nodeRoutePath.EnsureEndsWith('/') + "metaweblog").TrimStart('/');

            routes.Add(
                "articulate_metaweblog_" + routeHash,
                new Route(routePath, new MetaWeblogHandler()));
        }

        private static void MapRsdRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            var routeHash = nodeRoutePath.GetHashCode();

            //TODO: Make this work!

            //routes.MapRoute("articulate_rsd_" + routeHash,
            //            nodeRoutePath,
            //                new
            //            {
            //                controller = "Rsd",
            //                action = "Index",
            //                id = node.Id
            //            });
        }

        private static void MapManifestRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            var routeHash = nodeRoutePath.GetHashCode();

            var routePath = (nodeRoutePath + "wlwmanifest/{id}").TrimStart('/');

            //TODO: Make this work!

            //routes.MapRoute("articulate_wlwmanifest_" + routeHash,
            //           routePath,
            //           new
            //           {
            //               controller = "WlwManifest",
            //               action = "Index",
            //               id = node.Id
            //           });
        }

        private static void MapSearchRoute(RouteCollection routes, string nodeRoutePath, IPublishedContent[] nodesWithPath)
        {
            var routeHash = nodeRoutePath.GetHashCode();

            //TODO: Make this work!

            ////Create the route for the /search/{term} results
            //routes.MapUmbracoRoute(
            //    "articulate_search_" + routeHash,
            //    (nodeRoutePath.EnsureEndsWith('/') + node.GetPropertyValue<string>("searchUrlName") + "/{term}").TrimStart('/'),
            //    new
            //    {
            //        controller = "ArticulateSearch",
            //        action = "Search",
            //        term = UrlParameter.Optional
            //    },
            //    new ArticulateSearchRouteHandler(node.Id,
            //        node.GetPropertyValue<string>("searchUrlName"),
            //        node.GetPropertyValue<string>("searchPageName")));
        }

        private static void RemoveExisting(RouteCollection routes, params string[] names)
        {
            foreach (var name in names)
            {
                var r = routes[name];
                if (r != null)
                {
                    routes.Remove(r);
                }
            }
        }
    }
}