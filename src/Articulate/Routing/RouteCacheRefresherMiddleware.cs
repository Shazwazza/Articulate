using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    public class RouteCacheRefresherMiddleware : IMiddleware
    {
        private readonly IUmbracoContextFactory _umbracoContextFactory;
        private readonly IKeyValueService _keyValueService;
        private readonly ArticulateRouter _articulateRouter;
        private readonly CacheRefreshKey _appKey;

        public RouteCacheRefresherMiddleware(
            IUmbracoContextFactory umbracoContextFactory,
            IKeyValueService keyValueService,
            ArticulateRouter articulateRouter,
            CacheRefreshKey appKey)
        {
            _umbracoContextFactory = umbracoContextFactory ?? throw new ArgumentNullException(nameof(umbracoContextFactory));
            _keyValueService = keyValueService ?? throw new ArgumentNullException(nameof(keyValueService));
            _articulateRouter = articulateRouter ?? throw new ArgumentNullException(nameof(articulateRouter));
            _appKey = appKey;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (!context.Request.IsClientSideRequest() && !context.Request.IsBackOfficeRequest())
            {
                var keyVal = _keyValueService.GetValue(_appKey.Key);
                if (keyVal != null)
                {
                    if (DateTime.TryParse(keyVal, out var dt))
                    {
                        if (DateTime.UtcNow > dt)
                        {
                            using (var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext())
                            {
                                var umbCtx = umbracoContextReference.UmbracoContext;

                                // Regenerate the generated routes
                                _articulateRouter.MapRoutes(context, umbCtx);
                            }

                            // reset to max value so now is less than it so it doesn't trigger
                            _keyValueService.SetValue(_appKey.Key, DateTime.MaxValue.ToString("O"));
                        }
                    }
                    else
                    {
                        // reset invalid value
                        _keyValueService.SetValue(_appKey.Key, DateTime.MaxValue.ToString("O"));
                    } 
                }
            }

            await next(context);
        }
    }
}
