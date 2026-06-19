#nullable enable
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Routing
{
    internal static class ArticulateRouteSegmentHelper
    {
        private static string Normalize(string? routeSegment) => routeSegment?.Trim().Trim('/') ?? string.Empty;

        public static string? NormalizeOrNull(string? routeSegment)
        {
            var normalizedRouteSegment = Normalize(routeSegment);
            return normalizedRouteSegment.Length == 0 ? null : normalizedRouteSegment;
        }

        public static string? GetConfiguredSegment(IPublishedContent content, string propertyAlias) =>
            NormalizeOrNull(content.Value<string>(propertyAlias));

        public static string? CombineRoutePath(string rootNodePath, string? routeSegment)
        {
            var normalizedRouteSegment = NormalizeOrNull(routeSegment);
            return normalizedRouteSegment is null ? null : $"{rootNodePath}{normalizedRouteSegment}";
        }
    }
}
