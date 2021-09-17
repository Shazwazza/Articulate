using Articulate.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
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
        private readonly Dictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> _routeCache = new();
        private readonly IControllerActionSearcher _controllerActionSearcher;
        private static readonly string s_searchControllerName = ControllerExtensions.GetControllerName<ArticulateSearchController>();
        private static readonly string s_tagsControllerName = ControllerExtensions.GetControllerName<ArticulateTagsController>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="controllerActionSearcher"></param>
        public ArticulateRouter(IControllerActionSearcher controllerActionSearcher)
            => _controllerActionSearcher = controllerActionSearcher;

        public bool TryMatch(PathString path, RouteValueDictionary routeValues, out ArticulateRootNodeCache articulateRootNodeCache)
        {
            foreach(var item in _routeCache)
            {
                var templateMatcher = new TemplateMatcher(item.Key.RouteTemplate, routeValues);
                if (templateMatcher.TryMatch(path, routeValues))
                {
                    articulateRootNodeCache = item.Value;
                    return true;
                }
            }

            articulateRootNodeCache = null;
            return false;
        }

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

                var rootNodePath = nodeByPathGroup.Key.EnsureEndsWith('/');

                

                foreach (IPublishedContent articulateRootNode in nodeByPathGroup)
                {
                    //MapRssRoute(routes, uriPath, nodesAsArray)
                    //MapMarkdownEditorRoute(routes, uriPath, nodesAsArray);
                    //MapAuthorsRssRoute(routes, uriPath, nodesAsArray);

                    MapSearchRoute(httpContext, rootNodePath, articulateRootNode, domains);
                    //MapMetaWeblogRoute(routes, uriPath, articulateRootNode);
                    //MapManifestRoute(routes, uriPath, articulateRootNode);
                    //MapRsdRoute(routes, uriPath, articulateRootNode);
                    //MapOpenSearchRoute(routes, uriPath, articulateRootNode);

                    // tags/cats routes are the least specific
                    MapTagsAndCategoriesRoute(httpContext, rootNodePath, articulateRootNode, domains);
                }


            }
        }

        /// <summary>
        /// Generically caches a url path for a particular controller
        /// </summary>
        /// <param name="urlSegment"></param>
        /// <param name="httpContext"></param>
        /// <param name="rootNodePath"></param>
        /// <param name="articulateRootNode"></param>
        /// <param name="domains"></param>
        private void MapRoute(
            string controllerName,
            string actionName,
            RouteTemplate routeTemplate,
            HttpContext httpContext,
            IPublishedContent articulateRootNode,
            IReadOnlyList<Domain> domains)
        {
            var art = new ArticulateRouteTemplate(routeTemplate);
            if (!_routeCache.TryGetValue(art, out ArticulateRootNodeCache dynamicRouteValues))
            {
                ControllerActionDescriptor controllerActionDescriptor = _controllerActionSearcher.Find<IRenderController>(httpContext, controllerName, actionName);
                if (_controllerActionSearcher == null)
                {
                    throw new InvalidOperationException("No controller found with name " + controllerName);
                }
                dynamicRouteValues = new ArticulateRootNodeCache(controllerActionDescriptor);

                _routeCache[art] = dynamicRouteValues;
            }

            dynamicRouteValues.Add(articulateRootNode.Id, domains.Where(x => x.ContentId == articulateRootNode.Id).ToList());
        }

        private void MapSearchRoute(HttpContext httpContext, string rootNodePath, IPublishedContent articulateRootNode, IReadOnlyList<Domain> domains)
        {
            var searchUrlName = articulateRootNode.Value<string>("searchUrlName");
            RouteTemplate template = TemplateParser.Parse($"{rootNodePath}{searchUrlName}");
            MapRoute(
                s_searchControllerName,
                nameof(ArticulateSearchController.Search),
                template,
                httpContext,
                articulateRootNode,
                domains);
        }

        private void MapTagsAndCategoriesRoute(HttpContext httpContext, string rootNodePath, IPublishedContent articulateRootNode, IReadOnlyList<Domain> domains)
        {
            var categoriesUrlName = articulateRootNode.Value<string>("categoriesUrlName");
            RouteTemplate categoriesTemplate = TemplateParser.Parse($"{rootNodePath}{categoriesUrlName}/{{tag?}}");
            MapRoute(
                s_tagsControllerName,
                nameof(ArticulateTagsController.Categories),
                categoriesTemplate,
                httpContext,
                articulateRootNode,
                domains);

            var tagsUrlName = articulateRootNode.Value<string>("tagsUrlName");
            RouteTemplate tagsTemplate = TemplateParser.Parse($"{rootNodePath}{tagsUrlName}/{{tag?}}");
            MapRoute(
                s_tagsControllerName,
                nameof(ArticulateTagsController.Tags),
                tagsTemplate,
                httpContext,
                articulateRootNode,
                domains);

            // TODO: RSS for tags
        }
    }
}
