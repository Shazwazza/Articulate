using Articulate.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.Routing;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Mvc;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class ArticulateSearchRouteHandler : ArticulateVirtualNodeByIdRouteHandler
    {
        private struct UrlNames
        {
            public int NodeId { get; set; }
            public string SearchUrlName { get; set; }
            public string SearchPageName { get; set; }
        }

        private readonly List<UrlNames> _urlNames = new List<UrlNames>();

        public ArticulateSearchRouteHandler(UrlProvider umbracoUrlProvider, IEnumerable<IPublishedContent> itemsForRoute)
            : base(umbracoUrlProvider, itemsForRoute)
        {
            foreach (var node in itemsForRoute)
            {
                _urlNames.Add(new UrlNames
                {
                    NodeId = node.Id,
                    SearchUrlName = node.Value<string>("searchUrlName"),
                    SearchPageName = node.Value<string>("searchPageName")
                });
            }
        }        

        protected override IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext, IPublishedContent baseContent)
        {
            var urlNames = _urlNames.Single(x => x.NodeId == baseContent.Id);

            var controllerName = requestContext.RouteData.GetRequiredString("controller");
            var rootUrl = baseContent.Url;

            return new ArticulateVirtualPage(
                baseContent,
                urlNames.SearchPageName,
                controllerName,
                urlNames.SearchUrlName);
        }
    }
}