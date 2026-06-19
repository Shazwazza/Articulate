#nullable enable
using Articulate.Components;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Options;

namespace Articulate.Options
{
    /// <summary>
    ///     Wires up the Articulate view location expander with DI-provided dependencies.
    ///     Avoids using BuildServiceProvider at compose time.
    /// </summary>
    internal sealed class ConfigureArticulateRazorViewEngineOptions
        : IConfigureOptions<RazorViewEngineOptions>
    {
        /// <inheritdoc />
        public void Configure(RazorViewEngineOptions options) =>
            options.ViewLocationExpanders.Add(new ArticulateViewLocationExpander());
    }
}
