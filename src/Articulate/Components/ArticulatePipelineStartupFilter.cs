using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Articulate.Routing;

namespace Articulate.Components
{
    public class ArticulatePipelineStartupFilter : IConfigureOptions<UmbracoPipelineOptions>
    {
        public void Configure(UmbracoPipelineOptions options)
            => options.AddFilter(new UmbracoPipelineFilter(nameof(ArticulatePipelineStartupFilter))
            {
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    endpoints.MapDynamicControllerRoute<ArticulateRouteValueTransformer>(
                        "/{any}/{**slug}",
                        null,
                        100); // Ensure it runs AFTER Umbraco so that we can check if things are already matched.
                })
            });
    }
}
