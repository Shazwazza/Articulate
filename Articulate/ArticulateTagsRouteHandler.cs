using System;
using System.Globalization;
using System.Linq;
using System.Web.Routing;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Articulate
{
    public class ArticulateTagsRouteHandler : UmbracoVirtualNodeByIdRouteHandler
    {
        private readonly string _tagsUrlName;
        private readonly string _tagsPageName;
        private readonly string _categoriesUrlName;
        private readonly string _categoriesPageName;

        public ArticulateTagsRouteHandler(int realNodeId,
            string tagsUrlName,
            string tagsPageName,
            string categoriesUrlName,
            string categoriesPageName)
            : base(realNodeId)
        {
            _tagsUrlName = tagsUrlName;
            _tagsPageName = tagsPageName;
            _categoriesUrlName = categoriesUrlName;
            _categoriesPageName = categoriesPageName;
        }

        protected override IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext, IPublishedContent baseContent)
        {
            var tag = requestContext.RouteData.Values["tag"] == null ? null : requestContext.RouteData.Values["tag"].ToString();
            var actionName = requestContext.RouteData.GetRequiredString("action");
            var rootUrl = baseContent.Url;
            var urlName = actionName.InvariantEquals("tags") ? _tagsUrlName : _categoriesUrlName;
            var pageName = actionName.InvariantEquals("tags") ? _tagsPageName : _categoriesPageName;

            return new ArticulateVirtualPage(
                baseContent,
                tag.IsNullOrWhiteSpace() ? pageName : tag,
                requestContext.RouteData.GetRequiredString("controller"),
                tag.IsNullOrWhiteSpace()
                    ? rootUrl.EnsureEndsWith('/') + urlName
                    : rootUrl.EnsureEndsWith('/') + urlName.EnsureEndsWith('/') + tag);
        }
    }

}