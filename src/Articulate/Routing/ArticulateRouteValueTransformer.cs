using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Cms.Web.Website.Routing;
using static Umbraco.Cms.Core.Constants.Web.Routing;

namespace Articulate.Routing
{
    // ASP.NET Core no longer exposes a mutable RouteTable, so Articulate keeps its dynamic
    // route cache here and refreshes it when content changes.
    internal sealed class ArticulateRouteValueTransformer(
        IRuntimeState runtime,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedRouter publishedRouter,
        IRoutableDocumentFilter routableDocumentFilter,
        ArticulateRouter articulateRouteBuilder,
        IArticulateRouteRefreshState routeRefreshState,
        ILogger<ArticulateRouteValueTransformer> logger,
        UmbracoRouteValueTransformer umbracoRouteValueTransformer,
        IPublishedContentTypeCache publishedContentTypeCache,
        IDocumentCacheService documentCacheService)
        : DynamicRouteValueTransformer, IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private long _builtVersion;
        private bool _disposedValue;
        private volatile bool _hasCache;

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <inheritdoc />
        public override async ValueTask<RouteValueDictionary> TransformAsync(
            HttpContext httpContext,
            RouteValueDictionary values)
        {
            // check if Umbraco has already matched content, we don't want to execute if things
            // are already matching.
            if (!ShouldCheck(
                    httpContext,
                    out IUmbracoContext umbracoContext,
                    out UmbracoRouteValues umbracoRouteValues))
            {
                return [];
            }

            if (umbracoRouteValues is null)
            {
                // This can occur in Umbraco Cloud since some plugin that is used there prevents the UmbracoRouteValues from
                // being set the normal way. In this case, we'll need to force route it.
                RouteValueDictionary routeValues =
                    await umbracoRouteValueTransformer.TransformAsync(httpContext, values);

                umbracoRouteValues = httpContext.Features.Get<UmbracoRouteValues>();
                if (umbracoRouteValues is null)
                {
                    // Most likely will be null
                    return routeValues;
                }
            }

            var newValues = new RouteValueDictionary();

            EnsureRouteCache(umbracoContext, httpContext);

            var routeSuccess = await TryRouteAsync(umbracoContext, umbracoRouteValues, httpContext, newValues);

            return routeSuccess ? newValues : [];
        }

        private void EnsureRouteCache(IUmbracoContext umbracoContext, HttpContext httpContext)
        {
            var currentVersion = routeRefreshState.CurrentVersion;
            if (_hasCache && Volatile.Read(ref _builtVersion) == currentVersion)
            {
                return;
            }

            _lock.EnterWriteLock();
            try
            {
                currentVersion = routeRefreshState.CurrentVersion;
                if (_hasCache && _builtVersion == currentVersion)
                {
                    return;
                }

                logger.LogInformation(
                    "Rebuilding Articulate route cache. HasCache: {HasCache}, BuiltVersion: {BuiltVersion}, CurrentVersion: {CurrentVersion}, RequestPath: {RequestPath}",
                    _hasCache,
                    _builtVersion,
                    currentVersion,
                    httpContext.Request.Path.Value);

                articulateRouteBuilder.MapRoutes(
                    httpContext,
                    umbracoContext,
                    publishedContentTypeCache,
                    documentCacheService);
                _builtVersion = currentVersion;
                _hasCache = true;

                logger.LogInformation(
                    "Rebuilt Articulate route cache. BuiltVersion: {BuiltVersion}, RequestPath: {RequestPath}",
                    _builtVersion,
                    httpContext.Request.Path.Value);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposedValue)
            {
                return;
            }

            if (disposing)
            {
                _lock?.Dispose();
            }

            _disposedValue = true;
        }

        private async Task<bool> TryRouteAsync(
            IUmbracoContext umbracoContext,
            UmbracoRouteValues umbracoRouteValues,
            HttpContext httpContext,
            RouteValueDictionary values)
        {
            _lock.EnterReadLock();
            try
            {
                if (_hasCache)
                {
                    if (!articulateRouteBuilder.TryMatch(
                            httpContext.Request.Path,
                            values,
                            out ArticulateRootNodeCache dynamicRouteValues))
                    {
                        return false;
                    }

                    await WriteRouteValuesAsync(
                        umbracoContext,
                        httpContext,
                        dynamicRouteValues,
                        umbracoRouteValues,
                        values);
                    return true;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return false;
        }

        private async Task WriteRouteValuesAsync(
            IUmbracoContext umbracoContext,
            HttpContext httpContext,
            ArticulateRootNodeCache dynamicRouteValues,
            UmbracoRouteValues umbracoRouteValues,
            RouteValueDictionary values)
        {
            // Since we are executing after Umbraco's dynamic transformer, it means Umbraco has already gone ahead and matched a domain (if any). So we will use this to match our document.
            DomainAndUri assignedDomain = umbracoRouteValues?.PublishedRequest.Domain;

            // if we have a domain
            var contentId = dynamicRouteValues.GetContentId(assignedDomain);

            // No matching content for this domain - return early (route miss → 404)
            if (contentId == 0)
            {
                return;
            }

            IPublishedContent publishedContent = umbracoContext?.Content.GetById(contentId)
                                                 ?? throw new InvalidOperationException(
                                                     "Could not resolve content by id " + contentId);

            // instantiate, prepare and process the published content request important to use CleanedUmbracoUrl - lowercase path-only version of the current url
            IPublishedRequestBuilder requestBuilder =
                await publishedRouter.CreateRequestAsync(umbracoContext.CleanedUmbracoUrl);

            // re-assign the domain if there was one.
            if (assignedDomain != null)
            {
                _ = requestBuilder.SetDomain(assignedDomain);
            }

            _ = requestBuilder.SetPublishedContent(publishedContent);

            IPublishedRequest publishedRequest = requestBuilder.Build();

            umbracoRouteValues = new UmbracoRouteValues(
                publishedRequest,
                dynamicRouteValues.ControllerActionDescriptor);

            // Store the route values as a httpcontext feature
            httpContext.Features.Set(umbracoRouteValues);

            umbracoContext.PublishedRequest = publishedRequest;

            values[ControllerToken] = dynamicRouteValues.ControllerActionDescriptor.ControllerName;
            if (!string.IsNullOrWhiteSpace(dynamicRouteValues.ControllerActionDescriptor.ActionName))
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
            if (runtime.Level != RuntimeLevel.Run)
            {
                return false;
            }

            // will be null for any client side requests like JS, etc...
            if (!umbracoContextAccessor.TryGetUmbracoContext(out umbracoContext))
            {
                return false;
            }

            // If route values have already been assigned, then Umbraco has matched content, we will not proceed.
            // A 404 can be matched by Umbraco too which will occur for Articulate dynamic routes, so we need to
            // proceed to see if it is actually a 404.
            if (umbracoRouteValues?.PublishedRequest.PublishedContent is not null
                && umbracoRouteValues.PublishedRequest.ResponseStatusCode != 404)
            {
                return false;
            }

            return routableDocumentFilter.IsDocumentRequest(httpContext.Request.Path);
        }
    }
}
