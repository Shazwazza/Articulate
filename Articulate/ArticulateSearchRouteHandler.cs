using System.Web.Routing;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Articulate
{
    public class ArticulateSearchRouteHandler : UmbracoVirtualNodeByIdRouteHandler
    {
        private readonly string _searchUrlName;
        private readonly string _searchPageName;

        public ArticulateSearchRouteHandler(int realNodeId,
            string searchUrlName,
            string searchPageName)
            : base(realNodeId)
        {
            _searchUrlName = searchUrlName;
            _searchPageName = searchPageName;
        }

        protected override IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext, IPublishedContent baseContent)
        {
            var controllerName = requestContext.RouteData.GetRequiredString("controller");
            var rootUrl = baseContent.Url;

            return new ArticulateVirtualPage(
                baseContent,
                _searchPageName,
                controllerName,
                rootUrl.EnsureEndsWith('/') + _searchUrlName);
        }
    }
}