using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Articulate.Routing
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

        private readonly List<UrlNames> _urlNames;

        private const string DetectDomainAbsoluteUrl = "localhost";

        public TagsOrCategoryPathRouteConstraint(ContentUrls contentUrls, IEnumerable<IPublishedContent> itemsForRoute)
        {
            if (itemsForRoute == null) throw new ArgumentNullException(nameof(itemsForRoute));

            var urlNames = new List<UrlNames>();

            foreach (var node in itemsForRoute)
            {
                var allUrls = contentUrls.GetContentUrls(node);

                foreach (var url in allUrls)
                {
                    //if there is a double slash, it will have a domain
                    if (url.InvariantContains(DetectDomainAbsoluteUrl))
                    {
                        var uri = new Uri(url, UriKind.Absolute);
                        urlNames.Add(new UrlNames
                        {
                            Host = uri.Host,
                            CategoryUrlName = node.Value<string>("categoriesUrlName"),
                            TagsUrlName = node.Value<string>("tagsUrlName")
                        });
                    }
                    else
                    {
                        urlNames.Add(new UrlNames
                        {
                            Host = string.Empty,
                            CategoryUrlName = node.Value<string>("categoriesUrlName"),
                            TagsUrlName = node.Value<string>("tagsUrlName")
                        });
                    }
                }
            }

            _urlNames = urlNames;

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
                                       : !httpContext.Request.Url.AbsoluteUri.InvariantContains(DetectDomainAbsoluteUrl)
                        ? _urlNames.FirstOrDefault(x => x.Host == string.Empty)
                        : _urlNames.FirstOrDefault(x => x.Host.InvariantEquals(httpContext.Request.Url.Host));
            }

            if (urlNames == null) return false;

            var currentAction = values[parameterName].ToString();

            return currentAction.InvariantEquals(urlNames.TagsUrlName) || currentAction.InvariantEquals(urlNames.CategoryUrlName);
        }
    }
}
