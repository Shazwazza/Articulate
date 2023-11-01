using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    /// <summary>
    /// At the end of a request, we'll check if there is a flag in the request indicating to rebuild the routes
    /// </summary>
    /// <remarks>
    /// In some cases many articulate roots might be published at one time but we only want to rebuild the routes once so we'll do it once
    /// at the end of the request.
    /// </remarks>
    internal class RouteCacheRefresherFilter : IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext context) => PerformRefresh(context.HttpContext);

        public void OnActionExecuting(ActionExecutingContext context)
        {
        }

        private static void PerformRefresh(HttpContext context)
        {
            var appCaches = context.RequestServices.GetRequiredService<AppCaches>();

            if (appCaches.RequestCache.GetCacheItem<bool?>(ArticulateConstants.RefreshRoutesToken) == true)
            {
                var umbracoContextFactory = context.RequestServices.GetRequiredService<IUmbracoContextFactory>();
                var articulateRouter = context.RequestServices.GetRequiredService<ArticulateRouter>();

                using (var umbracoContextReference = umbracoContextFactory.EnsureUmbracoContext())
                {
                    var umbCtx = umbracoContextReference.UmbracoContext;

                    // Regenerate the generated routes
                    articulateRouter.MapRoutes(context, umbCtx);
                }
            }
        }
    }
}
