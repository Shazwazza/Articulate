#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Articulate.Services
{
    internal class ArticulateThemeResolver(
        IUmbracoContextAccessor umbracoContextAccessor,
        AppCaches appCaches,
        ILogger<ArticulateThemeResolver> logger)
        : IArticulateThemeResolver
    {
        /// <inheritdoc />
        public string? GetCurrentThemeName() =>

            // cache a single request.
            appCaches.RequestCache.GetCacheItem(
                "Articulate_CurrentRequestThemeName",
                () =>
                {
                    if (!umbracoContextAccessor.TryGetUmbracoContext(out IUmbracoContext? umbracoContext) ||
                        umbracoContext.PublishedRequest?.PublishedContent is null)
                    {
                        return string.Empty;
                    }

                    IPublishedContent articulateRoot =
                        umbracoContext.PublishedRequest.PublishedContent.AncestorOrSelf(ArticulateConstants.ContentType
                            .Articulate);

                    var themeName = articulateRoot.Value<string>("theme");
                    if (string.IsNullOrWhiteSpace(themeName))
                    {
                        logger.LogInformation(
                            "No theme has been set for the Articulate root node '{BlogName}'. Articulate theme view resolution will be bypassed.",
                            articulateRoot.Name);
                    }

                    return themeName?.StripHtml().StripNewLines().StripWhitespace() ?? string.Empty;
                });
    }
}
