using System.Web.Mvc;
using System.Web.Routing;

namespace Articulate
{
    public static class RouteCollectionExtensions
    {

        public static Route MapUmbracoRoute(this RouteCollection routes,
            string name, string url, object defaults, UmbracoVirtualNodeRouteHandler virtualNodeHandler, 
            object constraints = null, string[] namespaces = null)
        {
            var route = RouteTable.Routes.MapRoute(name, url, defaults, constraints, namespaces);
            route.RouteHandler = virtualNodeHandler;
            return route;
        }

    }
}