#nullable enable
using Articulate.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace Articulate.Components
{
    /// <summary>
    ///     Configuration for the Articulate dynamic routing in the middleware pipeline.
    /// </summary>
    public class ArticulatePipelineStartupFilter : IConfigureOptions<UmbracoPipelineOptions>
    {
        /// <inheritdoc />
        public void Configure(UmbracoPipelineOptions options)
            => options.AddFilter(new UmbracoPipelineFilter(nameof(ArticulatePipelineStartupFilter))
            {
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    endpoints.MapDynamicControllerRoute<ArticulateRouteValueTransformer>(
                        "/{any}/{**slug}",
                        null!,
                        1000); // Ensure it runs AFTER Umbraco so that we can check if things are already matched.
                })
            });
    }
}
