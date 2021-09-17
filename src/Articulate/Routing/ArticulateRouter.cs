using Articulate.Controllers;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Website.Routing;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    internal class ArticulateRouter
    {
        private readonly Dictionary<string, ArticulateRootNodeCache> _routeCache = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly IControllerActionSearcher _controllerActionSearcher;
        private static readonly string s_articulateSearchControllerName = ControllerExtensions.GetControllerName<ArticulateSearchController>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="controllerActionSearcher"></param>
        public ArticulateRouter(IControllerActionSearcher controllerActionSearcher)
            => _controllerActionSearcher = controllerActionSearcher;

        public bool TryMatch(string path, out ArticulateRootNodeCache articulateRootNodeCache)
            => _routeCache.TryGetValue(path, out articulateRootNodeCache);

        /// <summary>
        /// Builds all route caches.
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="umbracoContext"></param>
        /// <returns></returns>
        public void MapRoutes(HttpContext httpContext, IUmbracoContext umbracoContext)
        {
            IPublishedContentCache contentCache = umbracoContext.Content;

            IPublishedContentType articulateCt = contentCache.GetContentType("Articulate");
            if (articulateCt == null)
            {
                return;
            }

            var articulateNodes = contentCache.GetByContentType(articulateCt).ToList();

            var domains = umbracoContext.Domains.GetAll(false).ToList();

            // TODO: Enable this in some way
            //clear the existing articulate routes (if any)
            //RemoveExisting(routes);

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
                .GroupBy(x => RouteCollectionExtensions.RoutePathFromNodeUrl(httpContext, x.Url()))
                // This is required to ensure that we create routes that are more specific first
                // before creating routes that are less specific
                .OrderByDescending(x => x.Key.Split('/').Length);
            foreach (var nodeByPathGroup in articulateNodesGroupedByUriPath)
            {
                IPublishedContent[] nodesAsArray = nodeByPathGroup.ToArray();

                var uriPath = nodeByPathGroup.Key;

                //MapRssRoute(routes, uriPath, nodesAsArray);                
                //MapMarkdownEditorRoute(routes, uriPath, nodesAsArray);
                //MapAuthorsRssRoute(routes, uriPath, nodesAsArray);

                foreach (IPublishedContent articulateRootNode in nodeByPathGroup)
                {
                    MapSearchRoute(httpContext, uriPath, articulateRootNode, domains);
                    //MapMetaWeblogRoute(routes, uriPath, articulateRootNode);
                    //MapManifestRoute(routes, uriPath, articulateRootNode);
                    //MapRsdRoute(routes, uriPath, articulateRootNode);
                    //MapOpenSearchRoute(routes, uriPath, articulateRootNode);
                }

                // tags/cats routes are the least specific
                //MapTagsAndCategoriesRoute(routes, uriPath, nodesAsArray);
            }
        }

        private void MapSearchRoute(HttpContext httpContext, string nodeRoutePath, IPublishedContent articulateRootNode, IReadOnlyList<Domain> domains)
        {
            var searchUrlName = articulateRootNode.Value<string>("searchUrlName");
            var searchPath = nodeRoutePath.EnsureEndsWith('/') + searchUrlName;

            if (!_routeCache.TryGetValue(searchPath, out ArticulateRootNodeCache dynamicRouteValues))
            {
                dynamicRouteValues = new ArticulateRootNodeCache(_controllerActionSearcher.Find<IRenderController>(
                    httpContext,
                    s_articulateSearchControllerName,
                    nameof(ArticulateSearchController.Search)));
                _routeCache[searchPath] = dynamicRouteValues;
            }

            dynamicRouteValues.Add(articulateRootNode.Id, domains.Where(x => x.ContentId == articulateRootNode.Id).ToList());
        }
    }
}
