using System;
using System.Collections.Generic;
using Articulate.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    /// <summary>
    /// This is used purely so that RedirectToCurrentUmbracoPage works in SurfaceControllers with virtual nodes / custom routes.
    /// There may be more benefits to this but I'm not sure yet.
    /// </summary>
    internal class VirtualNodeUrlProvider : IUrlProvider
    {      

        /// <summary>
        /// Gets the nice url of a custom routed published content item
        /// </summary>
        public UrlInfo GetUrl(UmbracoContext umbracoContext, IPublishedContent content, UrlProviderMode mode, string culture, Uri current)
        {
            if (umbracoContext.PublishedRequest == null) return null;
            if (umbracoContext.PublishedRequest.PublishedContent == null) return null;
            var virtualPage = umbracoContext.PublishedRequest.PublishedContent as ArticulateVirtualPage;
            if (virtualPage == null) return null;

            //if the ids match, then return the assigned url
            return content.Id == virtualPage.Id ? UrlInfo.Url(virtualPage.Url, culture) : null;
        }

        /// <summary>
        /// The custom implementation returns null since we are not supporting url generation with this provider
        /// </summary>
        public IEnumerable<UrlInfo> GetOtherUrls(UmbracoContext umbracoContext, int id, Uri current)
        {
            return null;
        }
    }
}