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
            //NOTE: need to write lock because this might need to be remapped while the app is running if
            // any articulate nodes are updated with new values
            using (routes.GetWriteLock())
            {
                //find all articulate root nodes
                var articulateNodes = umbracoCache.GetByXPath("//Articulate");
                
                //for each one of them we need to create some virtual routes/nodes
                foreach (var node in articulateNodes)
                {
                    RemoveExisting(routes,
                        "articulate_tags_" + node.Id,
                        "articulate_search_" + node.Id,
                        "articulate_metaweblog_" + node.Id,
                        "articulate_rsd_" + node.Id,
                        "articulate_wlwmanifest_" + node.Id);

                    //Create the route for the /search/{term} results
                    routes.MapUmbracoRoute(
                        "articulate_search_" + node.Id,
                        (node.Url.EnsureEndsWith('/') + node.GetPropertyValue<string>("searchUrlName") + "/{term}").TrimStart('/'),
                        new
                        {
                            controller = "ArticulateSearch",
                            action = "Search",
                            term = UrlParameter.Optional
                        },
                        new ArticulateSearchRouteHandler(node.Id,
                            node.GetPropertyValue<string>("searchUrlName"),
                            node.GetPropertyValue<string>("searchPageName")));

                    routes.MapRoute("articulate_wlwmanifest_" + node.Id,
                        (node.Url.EnsureEndsWith('/') + "wlwmanifest/{id}").TrimStart('/'),
                        new
                        {
                            controller = "WlwManifest",
                            action = "Index",
                            id = node.Id
                        });

                    routes.MapRoute("articulate_rsd_" + node.Id,
                        (node.Url.EnsureEndsWith('/') + "rsd/{id}").TrimStart('/'),
                        new
                        {
                            controller = "Rsd",
                            action = "Index",
                            id = node.Id
                        });

                    routes.Add("articulate_metaweblog_" + node.Id,
                        new Route
                            (
                            (node.Url.EnsureEndsWith('/') + "metaweblog").TrimStart('/'),
                            new MetaWeblogHandler()
                            ));

                    //Create the routes for /tags/{tag} and /categories/{category}
                    routes.MapUmbracoRoute(
                        "articulate_tags_" + node.Id,
                        (node.Url.EnsureEndsWith('/') + "{action}/{tag}").TrimStart('/'),
                        new
                        {
                            controller = "ArticulateTags",
                            tag = UrlParameter.Optional
                        },
                        new ArticulateTagsRouteHandler(node.Id,
                            node.GetPropertyValue<string>("tagsUrlName"),
                            node.GetPropertyValue<string>("tagsPageName"),
                            node.GetPropertyValue<string>("categoriesUrlName"),
                            node.GetPropertyValue<string>("categoriesPageName")),
                        //Constraings: only match either the tags or categories url names
                        new { action = node.GetPropertyValue<string>("tagsUrlName") + "|" + node.GetPropertyValue<string>("categoriesUrlName") });

                    
                }
            }
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