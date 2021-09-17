using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Routing;
using static Umbraco.Cms.Core.Constants.Web.Routing;

namespace Articulate.Routing
{

    // TODO: We're going to need to do this for all dynamic routes so no more building routes
    // This is because there is no more RouteTable that you can write too so we sort of have to
    // re-create that here.
    internal class ArticulateRouteValueTransformer : DynamicRouteValueTransformer, IDisposable
    {
        private readonly IRuntimeState _runtime;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IPublishedRouter _publishedRouter;
        private readonly IRoutableDocumentFilter _routableDocumentFilter;
        private readonly ArticulateRouter _articulateRouter;
        private bool _hasCache = false;
        private bool _disposedValue;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public ArticulateRouteValueTransformer(
            IRuntimeState runtime,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedRouter publishedRouter,
            IRoutableDocumentFilter routableDocumentFilter,
            ArticulateRouter articulateRouteBuilder)
        {
            _runtime = runtime;
            _umbracoContextAccessor = umbracoContextAccessor;
            _publishedRouter = publishedRouter;
            _routableDocumentFilter = routableDocumentFilter;
            _articulateRouter = articulateRouteBuilder;
        }

        public override async ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
        {
            // check if Umbraco has already matched content, we don't want to execute if things
            // are already matching.
            if (!ShouldCheck(httpContext, out IUmbracoContext umbracoContext, out UmbracoRouteValues umbracoRouteValues))
            {
                return null;
            }

            var newValues = new RouteValueDictionary();

            (bool hasCache, bool routeSuccess) routeResult = await TryRoute(umbracoContext, umbracoRouteValues, httpContext, newValues);
            if (!routeResult.hasCache)
            {
                // we don't have a cache yet
                _lock.EnterWriteLock();
                try
                {
                    _articulateRouter.MapRoutes(httpContext, umbracoContext);
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
                    if (_articulateRouter.TryMatch(httpContext.Request.Path, values, out ArticulateRootNodeCache dynamicRouteValues))
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

        private async Task WriteRouteValues(IUmbracoContext umbracoContext, HttpContext httpContext, ArticulateRootNodeCache dynamicRouteValues, UmbracoRouteValues umbracoRouteValues, RouteValueDictionary values)
        {
            // Since we are executing after Umbraco's dynamic transformer, it means Umbraco has already
            // gone ahead and matched a domain (if any). So we will use this to match our document.
            int contentId = dynamicRouteValues.GetContentId(umbracoRouteValues.PublishedRequest?.Domain);

            IPublishedContent publishedContent = umbracoContext.Content.GetById(contentId);
            if (publishedContent == null)
            {
                throw new InvalidOperationException("Could not resolve content by id " + contentId);
            }

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
