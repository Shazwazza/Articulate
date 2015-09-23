using System;
using System.Collections.Generic;
using Articulate.Models;
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
        public string GetUrl(UmbracoContext umbracoContext, int id, Uri current, UrlProviderMode mode)
        {
            if (umbracoContext.PublishedContentRequest == null) return null;
            if (umbracoContext.PublishedContentRequest.PublishedContent == null) return null;
            var virtualPage = umbracoContext.PublishedContentRequest.PublishedContent as ArticulateVirtualPage;
            if (virtualPage == null) return null;
            //if the ids match, then return the assigned url
            return id == virtualPage.Id ? virtualPage.Url : null;
        }

        /// <summary>
        /// The custom implementation returns null since we are not supporting url generation with this provider
        /// </summary>        
        public IEnumerable<string> GetOtherUrls(UmbracoContext umbracoContext, int id, Uri current)
        {
            return null;
        }
    }
}