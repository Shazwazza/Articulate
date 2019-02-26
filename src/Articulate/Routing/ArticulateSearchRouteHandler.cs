using System.Collections.Generic;
using System.Linq;
using System.Web.Routing;
using Articulate.Models;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Articulate.Routing
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

        public ArticulateSearchRouteHandler(ILogger logger, ContentUrls contentUrls, IEnumerable<IPublishedContent> itemsForRoute)
            : base(logger, contentUrls, itemsForRoute)
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