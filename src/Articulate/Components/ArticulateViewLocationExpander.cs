#nullable enable
using Articulate.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Articulate.Components
{
    internal class ArticulateViewLocationExpander : IViewLocationExpander
    {
        private const string ThemeKey = "articulate-theme";
        private const string ThemeItemsKey = "ThemeName";

        /// <inheritdoc />
        public IEnumerable<string> ExpandViewLocations(
            ViewLocationExpanderContext context,
            IEnumerable<string> viewLocations)
        {
            IDictionary<string, string?> values = context.Values;

            _ = values.TryGetValue(ThemeKey, out var themeName);

            if (string.IsNullOrEmpty(themeName))
            {
                // Fallback: try HttpContext.Items when Values is unavailable (e.g., in unit tests)
                HttpContext httpContextForItems = context.ActionContext.HttpContext;
                if (httpContextForItems.Items[ThemeItemsKey] is string detectedThemeName)
                {
                    themeName = detectedThemeName;
                }
            }

            if (string.IsNullOrEmpty(themeName))
            {
                ILogger<ArticulateViewLocationExpander>? logger = context.ActionContext.HttpContext.RequestServices
                    .GetService<ILogger<ArticulateViewLocationExpander>>();
                logger?.LogDebug(
                    "No Articulate theme specified. Bypassing Articulate theme engine and falling back to standard view locations.");
                return viewLocations;
            }

            HttpContext httpContext = context.ActionContext.HttpContext;

            IArticulateViewLocationProvider? locationProvider =
                httpContext.RequestServices.GetService<IArticulateViewLocationProvider>();
            if (locationProvider is null)
            {
                return viewLocations;
            }

            IEnumerable<string> themeLocations = locationProvider.GetLocations(themeName);
            return themeLocations.Concat(viewLocations);
        }

        /// <inheritdoc />
        public void PopulateValues(ViewLocationExpanderContext context)
        {
            HttpContext httpContext = context.ActionContext.HttpContext;

            IArticulateThemeResolver? themeResolver =
                httpContext.RequestServices.GetService<IArticulateThemeResolver>();
            var themeName = themeResolver?.GetCurrentThemeName() ?? string.Empty;

            // Values may be null in unit testing scenarios when constructed directly.
            IDictionary<string, string?> values = context.Values;
            values[ThemeKey] = themeName;

            if (string.IsNullOrWhiteSpace(themeName))
            {
                _ = httpContext.Items.Remove(ThemeItemsKey);
            }
            else
            {
                httpContext.Items[ThemeItemsKey] = themeName;
            }
        }
    }
}
