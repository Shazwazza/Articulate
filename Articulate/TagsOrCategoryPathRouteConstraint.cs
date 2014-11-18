using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Articulate
{
    /// <summary>
    /// A route constraint that matches only the specified categories or tag route path names
    /// </summary>
    public sealed class TagsOrCategoryPathRouteConstraint : IRouteConstraint
    {
        private struct UrlNames
        {
            public string Host { get; set; }
            public string TagsUrlName { get; set; }
            public string CategoryUrlName { get; set; }
        }

        private readonly List<UrlNames> _urlNames = new List<UrlNames>();

        public TagsOrCategoryPathRouteConstraint(IEnumerable<IPublishedContent> itemsForRoute)
        {
            foreach (var node in itemsForRoute)
            {
                var url = node.Url;
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

        public bool Match(HttpContextBase httpContext,Route route,string parameterName,RouteValueDictionary values,RouteDirection routeDirection)
        {
            //determine if it's for a particular domain
            UrlNames urlNames;
            if (_urlNames.Count == 1)
            {
                urlNames = _urlNames.First();
            }
            else
            {
                urlNames = httpContext.Request.Url == null
                    ? _urlNames.First()  //cannot be determined
                    : httpContext.Request.Url.Host.InvariantEquals("localhost")
                        ? _urlNames.First(x => x.Host == string.Empty)
                        : _urlNames.First(x => x.Host.InvariantEquals(httpContext.Request.Url.Host));
            }

            var currentAction = values[parameterName].ToString();

            return currentAction.InvariantEquals(urlNames.TagsUrlName) || currentAction.InvariantEquals(urlNames.CategoryUrlName);
        }
    }
}