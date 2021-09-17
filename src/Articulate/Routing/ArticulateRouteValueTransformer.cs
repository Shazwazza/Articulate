using Articulate.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Cms.Web.Website.Routing;
using Umbraco.Extensions;
using static Umbraco.Cms.Core.Constants.Web.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace Articulate.Routing
{
    // WE can do routes dynamically, see https://ww.mariusgundersen.net/dynamic-endpoint-routing/
    // BUT! Everything is massively internal in dotnet. 

    //internal static class ArticulateEndpointExtensions
    //{
    //    public static void UseArticulateEndpoints(this IEndpointRouteBuilder endpoints)
    //    {
    //        ArticulateEndpointDataSource dataSource = endpoints.ServiceProvider.GetService<ArticulateEndpointDataSource>();

    //        if (dataSource is null)
    //        {
    //            throw new Exception("Did you forget to add MyEndpointDataSource to the services?");
    //        }

    //        endpoints.DataSources.Add(dataSource);
    //    }
    //}

    //internal class ArticulateEndpointDataSource : EndpointDataSource
    //{
    //    private readonly object _lock = new object();
    //    private IReadOnlyList<Endpoint> _endpoints;
    //    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    //    private IChangeToken _changeToken;

    //    public ArticulateEndpointDataSource()
    //    {
    //        _endpoints = new List<Endpoint>();
    //        _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);
    //    }

    //    public override IChangeToken GetChangeToken() => _changeToken;

    //    public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

    //    public void SetEndpoints(IReadOnlyList<Endpoint> endpoints)
    //    {
    //        lock (_lock)
    //        {
    //            var oldCancellationTokenSource = _cancellationTokenSource;

    //            _endpoints = endpoints;

    //            _cancellationTokenSource = new CancellationTokenSource();
    //            _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);

    //            oldCancellationTokenSource?.Cancel();
    //        }
    //    }

    //    public static Endpoint CreateEndpoint(string pattern, RequestDelegate requestDelegate) =>
    //        new RouteEndpointBuilder(
    //            requestDelegate: requestDelegate,
    //            routePattern: RoutePatternFactory.Parse(pattern),
    //            order: 0)
    //        .Build();
    //}

    //public class ArticulateSelectorPolicy : MatcherPolicy, IEndpointSelectorPolicy
    //{
    //    public override int Order => 0;

    //    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    //    {
    //        return true;
    //    }

    //    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    //    {
    //        return Task.CompletedTask;
    //        //throw new NotImplementedException();
    //    }
    //}

    public class DynamicRouteValues
    {
        public DynamicRouteValues(int nodeId, ControllerActionDescriptor controllerActionDescriptor)
        {
            NodeId = nodeId;
            ControllerActionDescriptor = controllerActionDescriptor;
        }

        public int NodeId { get; }
        public ControllerActionDescriptor ControllerActionDescriptor { get; }
    }

    internal class ArticulateRouteValueTransformer2 : ArticulateRouteValueTransformer
    {
        public ArticulateRouteValueTransformer2(IRuntimeState runtime, IUmbracoContextAccessor umbracoContextAccessor, IPublishedRouter publishedRouter, IRoutableDocumentFilter routableDocumentFilter, IControllerActionSearcher controllerActionSearcher) : base(runtime, umbracoContextAccessor, publishedRouter, routableDocumentFilter, controllerActionSearcher)
        {
        }

        public override async ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
        {
            var result = await base.TransformAsync(httpContext, values);
            
            result[ActionToken] = "Search";
            result["term"] = "blah";

            return result;
        }
    }

    // TODO: We're going to need to do this for all dynamic routes so no more building routes
    // This is because there is no more RouteTable that you can write too so we sort of have to
    // re-create that here.
    internal class ArticulateRouteValueTransformer : DynamicRouteValueTransformer, IDisposable
    {
        private readonly IRuntimeState _runtime;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IPublishedRouter _publishedRouter;
        private readonly IRoutableDocumentFilter _routableDocumentFilter;

        //private readonly IUmbracoRouteValuesFactory _routeValuesFactory;
        private readonly IControllerActionSearcher _controllerActionSearcher;
        private Dictionary<string, DynamicRouteValues> _routeCache = new Dictionary<string, DynamicRouteValues>();
        private bool _hasCache = false;
        private bool _disposedValue;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private static readonly string s_articulateSearchControllerName = ControllerExtensions.GetControllerName<ArticulateSearchController>();

        public ArticulateRouteValueTransformer(
            IRuntimeState runtime,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedRouter publishedRouter,
            IRoutableDocumentFilter routableDocumentFilter,
            //IUmbracoRouteValuesFactory routeValuesFactory,
            IControllerActionSearcher controllerActionSearcher)
        {
            _runtime = runtime;
            _umbracoContextAccessor = umbracoContextAccessor;
            _publishedRouter = publishedRouter;
            _routableDocumentFilter = routableDocumentFilter;
            //_routeValuesFactory = routeValuesFactory;
            _controllerActionSearcher = controllerActionSearcher;
        }

        public override ValueTask<IReadOnlyList<Endpoint>> FilterAsync(HttpContext httpContext, RouteValueDictionary values, IReadOnlyList<Endpoint> endpoints)
        {
            return base.FilterAsync(httpContext, values, endpoints);
        }

        public override async ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
        {
            if (!ShouldCheck(httpContext, out IUmbracoContext umbracoContext, out UmbracoRouteValues umbracoRouteValues))
            {
                return null;
            }

            var newValues = new RouteValueDictionary();
            //newValues[ActionToken] = "Index";
            //newValues[ControllerToken] = s_articulateSearchControllerName;
            //return newValues;

            var routeResult = await TryRoute(umbracoContext, umbracoRouteValues, httpContext, newValues);
            if (!routeResult.hasCache)
            {
                // we don't have a cache yet
                _lock.EnterWriteLock();
                try
                {
                    MapRoutes(httpContext, umbracoContext);
                    _hasCache = true;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
                routeResult = await TryRoute(umbracoContext, umbracoRouteValues, httpContext, newValues);
            }

            return routeResult.routeSuccess ? newValues : null;
        }

        private async Task<(bool hasCache, bool routeSuccess)> TryRoute(IUmbracoContext umbracoContext, UmbracoRouteValues umbracoRouteValues, HttpContext httpContext, RouteValueDictionary values)
        {
            _lock.EnterReadLock();
            try
            {
                if (_hasCache)
                {
                    if (_routeCache.TryGetValue(httpContext.Request.Path, out DynamicRouteValues dynamicRouteValues))
                    {
                        await WriteRouteValues(umbracoContext, httpContext, dynamicRouteValues, umbracoRouteValues, values);
                        return (true, true);
                    }

                    return (true, false);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
            return (false, false);
        }

        private async Task WriteRouteValues(IUmbracoContext umbracoContext, HttpContext httpContext, DynamicRouteValues dynamicRouteValues, UmbracoRouteValues umbracoRouteValues, RouteValueDictionary values)
        {
            IPublishedContent publishedContent = umbracoContext.Content.GetById(dynamicRouteValues.NodeId);
            // TODO: Check null

            // instantiate, prepare and process the published content request
            // important to use CleanedUmbracoUrl - lowercase path-only version of the current url
            IPublishedRequestBuilder requestBuilder = await _publishedRouter.CreateRequestAsync(umbracoContext.CleanedUmbracoUrl);

            // TODO: How are we going to do this? We need to borrow a bunch of code from core.
            //if (umbracoRouteValues.PublishedRequest.Domain != null)
            //{
            //    requestBuilder.SetDomain(umbracoRouteValues.PublishedRequest.Domain);
            //}

            requestBuilder.SetPublishedContent(publishedContent);


            IPublishedRequest publishedRequest = requestBuilder.Build();

            umbracoRouteValues = new UmbracoRouteValues(
                publishedRequest,
                dynamicRouteValues.ControllerActionDescriptor);

            // Store the route values as a httpcontext feature
            httpContext.Features.Set(umbracoRouteValues);

            values[ControllerToken] = dynamicRouteValues.ControllerActionDescriptor.ControllerName;
            if (string.IsNullOrWhiteSpace(dynamicRouteValues.ControllerActionDescriptor.ActionName) == false)
            {
                values[ActionToken] = dynamicRouteValues.ControllerActionDescriptor.ActionName;
            }
        }

        private void MapRoutes(HttpContext httpContext, IUmbracoContext umbracoContext)
        {
            IPublishedContentCache contentCache = umbracoContext.Content;

            IPublishedContentType articulateCt = contentCache.GetContentType("Articulate");
            if (articulateCt == null)
            {
                return;
            }

            var articulateNodes = contentCache.GetByContentType(articulateCt).ToList();

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
                MapSearchRoute(httpContext, uriPath, nodesAsArray);
                //MapMarkdownEditorRoute(routes, uriPath, nodesAsArray);
                //MapAuthorsRssRoute(routes, uriPath, nodesAsArray);

                foreach (IPublishedContent articulateRootNode in nodeByPathGroup)
                {
                    //MapMetaWeblogRoute(routes, uriPath, articulateRootNode);
                    //MapManifestRoute(routes, uriPath, articulateRootNode);
                    //MapRsdRoute(routes, uriPath, articulateRootNode);
                    //MapOpenSearchRoute(routes, uriPath, articulateRootNode);
                }

                // tags/cats routes are the least specific
                //MapTagsAndCategoriesRoute(routes, uriPath, nodesAsArray);
            }
        }

        private void MapSearchRoute(HttpContext httpContext, string nodeRoutePath, IPublishedContent[] searchNodes)
        {
            // we need to group by the search url name and make unique routes amongst those,
            // alternatively we could create route constraints like we do for the tags/categories routes
            foreach (var nodeSearch in searchNodes.GroupBy(x => x.Value<string>("searchUrlName")))
            {
                //the hash needs to be the combination of the nodeRoutePath and the searchUrl group
                //var routeHash = (nodeRoutePath + nodeSearch.Key).GenerateHash();

                var searchPath = nodeRoutePath.EnsureEndsWith('/') + nodeSearch.Key;

                _routeCache.Add(searchPath, new DynamicRouteValues(
                    // TODO: Fix this Id
                    nodeSearch.First().Id,
                    _controllerActionSearcher.Find<IRenderController>(
                        httpContext,
                        s_articulateSearchControllerName,
                        nameof(ArticulateSearchController.Search))));

                ////Create the route for the /search/{term} results
                //routes.MapUmbracoRoute(
                //    "articulate_search_" + routeHash,
                //    (nodeRoutePath.EnsureEndsWith('/') + nodeSearch.Key + "/{term}").TrimStart('/'),
                //    new
                //    {
                //        controller = "ArticulateSearch",
                //        action = "Search",
                //        term = UrlParameter.Optional
                //    },
                //    new ArticulateSearchRouteHandler(_logger, _contentUrls, nodesWithPath));
            }
        }

        private bool ShouldCheck(
            HttpContext httpContext,
            out IUmbracoContext umbracoContext,
            out UmbracoRouteValues umbracoRouteValues)
        {
            umbracoRouteValues = httpContext.Features.Get<UmbracoRouteValues>();

            umbracoContext = null;

            // If we aren't running, then we have nothing to route
            if (_runtime.Level != RuntimeLevel.Run)
            {
                return false;
            }
            // will be null for any client side requests like JS, etc...
            if (!_umbracoContextAccessor.TryGetUmbracoContext(out umbracoContext))
            {
                return false;
            }

            // If route values have already been assigned, then Umbraco has
            // matched content, we will not proceed.            
            if (umbracoRouteValues?.PublishedRequest?.PublishedContent != null)
            {
                return false;
            }

            if (!_routableDocumentFilter.IsDocumentRequest(httpContext.Request.Path))
            {
                return false;
            }

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _lock.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose() => Dispose(disposing: true);
    }
}
