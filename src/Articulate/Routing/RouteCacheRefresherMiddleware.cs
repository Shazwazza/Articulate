using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    public class RouteCacheRefresherMiddleware : IMiddleware
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IKeyValueService _keyValueService;
        private readonly ArticulateRouter _articulateRouter;

        public RouteCacheRefresherMiddleware(
            IUmbracoContextAccessor umbracoContextAccessor,
            IKeyValueService keyValueService,
            ArticulateRouter articulateRouter)
        {
            _umbracoContextAccessor = umbracoContextAccessor ?? throw new ArgumentNullException(nameof(umbracoContextAccessor));
            _keyValueService = keyValueService ?? throw new ArgumentNullException(nameof(keyValueService));
            _articulateRouter = articulateRouter ?? throw new ArgumentNullException(nameof(articulateRouter));
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (!context.Request.IsClientSideRequest() && !context.Request.IsBackOfficeRequest())
            {
                var keyVal = _keyValueService.GetValue("Articulate.CacheRefresh");
                if (keyVal != null)
                {
                    if (DateTime.TryParse(keyVal, out var dt))
                    {
                        if (DateTime.UtcNow > dt)
                        {
                            // Regenerate the generated routes
                            _articulateRouter.MapRoutes(context, _umbracoContextAccessor.GetRequiredUmbracoContext());
                        }
                    }
                    else
                    {
                        // needs to be set correctly, max value so now is less than it so it doesn't trigger
                        _keyValueService.SetValue("Articulate.CacheRefresh", DateTime.MaxValue.ToString("O"));
                    }
                }
            }

            await next(context);
        }
    }
}
