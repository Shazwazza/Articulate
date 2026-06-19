#nullable enable
using Microsoft.AspNetCore.Http;

namespace Articulate.Routing
{
    /// <summary>
    ///     Extension methods for route collections.
    /// </summary>
    public static class RouteCollectionExtensions
    {
        /// <summary>
        ///     Returns a route path from a given node's URL since a node's Url might contain a domain which we can't use in our
        ///     routing.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <param name="routePath">The route path or URL to convert.</param>
        /// <returns>A virtual path suitable for routing.</returns>
        internal static string RoutePathFromNodeUrl(HttpContext httpContext, string routePath)
        {
            var virtualPath =
                $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";

            var rootRoutePath = (Uri.TryCreate(routePath, UriKind.Absolute, out Uri? result)
                ? result.PathAndQuery
                : routePath).EnsureEndsWith('/');

            if (rootRoutePath == virtualPath)
            {
                return string.Empty;
            }

            return rootRoutePath.StartsWith(virtualPath)
                ? rootRoutePath[virtualPath.Length..]
                : rootRoutePath.TrimStart('/');
        }
    }
}
