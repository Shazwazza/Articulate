using System;
using System.Collections.Generic;
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
        private struct UrlAndPageNames
        {
            public int NodeId { get; set; }
            public string TagsUrlName { get; set; }
            public string TagsPageName { get; set; }
            public string CategoriesUrlName { get; set; }
            public string CategoriesPageName { get; set; }
        }

        private readonly List<UrlAndPageNames> _urlsAndPageNames = new List<UrlAndPageNames>(); 

        /// <summary>
        /// Constructor used to create a new handler for multi-tenency with domains and ids
        /// </summary>
        /// <param name="itemsForRoute"></param>
        public ArticulateTagsRouteHandler(IEnumerable<IPublishedContent> itemsForRoute)
            : base(itemsForRoute)
        {
            foreach (var node in itemsForRoute)
            {
                _urlsAndPageNames.Add(new UrlAndPageNames
                {
                    NodeId = node.Id,
                    TagsUrlName = node.GetPropertyValue<string>("tagsUrlName"),
                    TagsPageName = node.GetPropertyValue<string>("tagsPageName"),
                    CategoriesUrlName = node.GetPropertyValue<string>("categoriesUrlName"),
                    CategoriesPageName = node.GetPropertyValue<string>("categoriesPageName")
                });
            }
        }

        /// <summary>
        /// Constructor used to create a new handler for only one id and no domain
        /// </summary>
        /// <param name="realNodeId"></param>
        /// <param name="tagsUrlName"></param>
        /// <param name="tagsPageName"></param>
        /// <param name="categoriesUrlName"></param>
        /// <param name="categoriesPageName"></param>
        public ArticulateTagsRouteHandler(int realNodeId,
            string tagsUrlName,
            string tagsPageName,
            string categoriesUrlName,
            string categoriesPageName)
            : base(realNodeId)
        {
            _urlsAndPageNames.Add(new UrlAndPageNames
            {
                CategoriesPageName = categoriesPageName,
                CategoriesUrlName = categoriesUrlName,
                NodeId = realNodeId,
                TagsPageName = tagsPageName,
                TagsUrlName = tagsUrlName
            });
        }

        protected override IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext, IPublishedContent baseContent)
        {
            var urlAndPageName = _urlsAndPageNames.Single(x => x.NodeId == baseContent.Id);

            var tag = requestContext.RouteData.Values["tag"] == null ? null : requestContext.RouteData.Values["tag"].ToString();
            var actionName = requestContext.RouteData.GetRequiredString("action");
            var rootUrl = baseContent.Url;
            var urlName = actionName.InvariantEquals("tags") ? urlAndPageName.TagsUrlName : urlAndPageName.CategoriesUrlName;
            var pageName = actionName.InvariantEquals("tags") ? urlAndPageName.TagsPageName : urlAndPageName.CategoriesPageName;

            return new ArticulateVirtualPage(
                baseContent,
                tag.IsNullOrWhiteSpace() ? pageName : tag,
                requestContext.RouteData.GetRequiredString("controller"),
                tag.IsNullOrWhiteSpace()
                    ? urlName
                    : urlName.EnsureEndsWith('/') + tag);
        }
    }

}