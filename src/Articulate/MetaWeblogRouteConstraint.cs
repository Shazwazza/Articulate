using System.Web;
using System.Web.Routing;
using Umbraco.Core;

namespace Articulate
{
    /// <summary>
    /// Used for the metaweblog route so that there are some constraints applied.
    /// </summary>
    /// <remarks>
    /// Since the metaweblog route is a custom route (not mvc, etc...) we don't want it to take part in any url generation, we just
    /// want it to be routed to. So we always return false for url gen and then just have a very rudimentary check with the current url path
    /// </remarks>
    public sealed class MetaWeblogRouteConstraint : IRouteConstraint
    {
        /// <summary>
        /// Determines whether the URL parameter contains a valid value for this constraint.
        /// </summary>
        /// <returns>
        /// true if the URL parameter contains a valid value; otherwise, false.
        /// </returns>
        /// <param name="httpContext">An object that encapsulates information about the HTTP request.</param><param name="route">The object that this constraint belongs to.</param><param name="parameterName">The name of the parameter that is being checked.</param><param name="values">An object that contains the parameters for the URL.</param><param name="routeDirection">An object that indicates whether the constraint check is being performed when an incoming request is being handled or when a URL is being generated.</param>
        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            switch (routeDirection)
            {
                case RouteDirection.IncomingRequest:
                    return httpContext.Request.RawUrl.InvariantContains("/metaweblog/");
                case RouteDirection.UrlGeneration:
                default:
                    return false;
            }
        }
    }
}