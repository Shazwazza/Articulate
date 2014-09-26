namespace Articulate
{
    using System;
    using System.Web.Mvc;
    using System.Web.Routing;

    public static class RouteCollectionExtensions
    {
        public static Route MapUmbracoRoute(this RouteCollection routes,
            string name, string url, object defaults, UmbracoVirtualNodeRouteHandler virtualNodeHandler, 
            object constraints = null, string[] namespaces = null)
        {
            url = EnsureRoutePath(url);

            var route = RouteTable.Routes.MapRoute(name, url, defaults, constraints, namespaces);
            route.RouteHandler = virtualNodeHandler;
            return route;
        }

        internal static string EnsureRoutePath(string routePath)
        {
            Uri result;
            return Uri.TryCreate(routePath, UriKind.Absolute, out result) ? result.PathAndQuery.Substring(1) : routePath;
        }
    }
}