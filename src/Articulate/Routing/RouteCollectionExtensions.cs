using System;
using Microsoft.AspNetCore.Http;
using Umbraco.Extensions;

namespace Articulate.Routing
{
    public static class RouteCollectionExtensions
    {
        /// <summary>
        /// Returns a route path from a given node's URL since a node's Url might contain a domain which we can't use in our routing.
        /// </summary>
        /// <param name="routePath"></param>
        /// <returns></returns>
        internal static string RoutePathFromNodeUrl(HttpContext httpContext, string routePath)
        {
            var virtualPath = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";

            var rootRoutePath = (Uri.TryCreate(routePath, UriKind.Absolute, out Uri result)
                ? result.PathAndQuery
                : routePath).EnsureEndsWith('/');

            if (rootRoutePath == virtualPath)
                return string.Empty;

            return rootRoutePath.StartsWith(virtualPath)
                ? rootRoutePath.Substring(virtualPath.Length)
                : rootRoutePath.TrimStart('/');
        }
    }
}
