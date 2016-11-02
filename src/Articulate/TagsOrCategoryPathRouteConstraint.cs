using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    /// <summary>
    /// A route constraint that matches only the specified categories or tag route path names
    /// </summary>
    public sealed class TagsOrCategoryPathRouteConstraint : IRouteConstraint
    {
        [DebuggerDisplay("Host: {Host}, TagsUrlName: {TagsUrlName}, CategoryUrlName: {CategoryUrlName}")]
        private class UrlNames
        {
            public string Host { get; set; }
            public string TagsUrlName { get; set; }
            public string CategoryUrlName { get; set; }
        }

        private readonly List<UrlNames> _urlNames = new List<UrlNames>();

        public TagsOrCategoryPathRouteConstraint(UrlProvider umbracoUrlProvider, IEnumerable<IPublishedContent> itemsForRoute)
        {
            if (itemsForRoute == null) throw new ArgumentNullException(nameof(itemsForRoute));

            foreach (var node in itemsForRoute)
            {
                var allUrls = ArticulateRoutes.GetContentUrls(umbracoUrlProvider, node);

                foreach (var url in allUrls)
                {
                    //if there is a double slash, it will have a domain
                    if (url.Contains("//"))
                    {
                        var uri = new Uri(url, UriKind.Absolute);
                        _urlNames.Add(new UrlNames
                        {
                            Host = uri.Host,
                            CategoryUrlName = node.GetPropertyValue<string>("categoriesUrlName"),
                            TagsUrlName = node.GetPropertyValue<string>("tagsUrlName")
                        });
                    }
                    else
                    {
                        _urlNames.Add(new UrlNames
                        {
                            Host = string.Empty,
                            CategoryUrlName = node.GetPropertyValue<string>("categoriesUrlName"),
                            TagsUrlName = node.GetPropertyValue<string>("tagsUrlName")
                        });
                    }
                }
            }
        }

        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            //if this is an articulate root path, then we cannot match!

            //determine if it's for a particular domain
            UrlNames urlNames;
            if (_urlNames.Count == 1)
            {
                urlNames = _urlNames.FirstOrDefault();
            }
            else
            {
                urlNames = httpContext.Request.Url == null
                    ? _urlNames.FirstOrDefault()  //cannot be determined
                                                  //TODO: Why is this checking for UseDomainPrefixes + localhost? I can't figure that part out (even though i wrote that)
                    : httpContext.Request.Url.Host.InvariantEquals("localhost") && !UmbracoConfig.For.UmbracoSettings().RequestHandler.UseDomainPrefixes
                        ? _urlNames.FirstOrDefault(x => x.Host == string.Empty)
                        : _urlNames.FirstOrDefault(x => x.Host.InvariantEquals(httpContext.Request.Url.Host));
            }

            if (urlNames == null) return false;

            var currentAction = values[parameterName].ToString();

            return currentAction.InvariantEquals(urlNames.TagsUrlName) || currentAction.InvariantEquals(urlNames.CategoryUrlName);
        }
    }
}