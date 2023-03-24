using System;
using System.Linq;
using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the Articulate root node as the main blog post list by date
    /// </summary>
    public class ArticulateController : ListControllerBase
    {
        private readonly UmbracoHelper _umbracoHelper;

        public ArticulateController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedUrlProvider publishedUrlProvider,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            UmbracoHelper umbracoHelper)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider, publishedValueFallback, variationContextAccessor)
        {
            _umbracoHelper = umbracoHelper;
        }

        /// <summary>
        /// Declare new Index action with optional page number
        /// </summary>
        /// <param name="model"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Index(int? p) => RenderView(new ContentModel(CurrentPage), p);

        /// <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [NonAction]
        public override IActionResult Index() => Index(0);

        private IActionResult RenderView(ContentModel model, int? p = null)
        {
            var listNodes = model.Content.ChildrenOfType(ArticulateConstants.ArticulateArchiveContentTypeAlias).ToArray();
            if (listNodes.Length == 0)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            var master = new MasterModel(model.Content, PublishedValueFallback, VariationContextAccessor);

            var count = _umbracoHelper.GetPostCount(listNodes.Select(x => x.Id).ToArray());

            var posts = _umbracoHelper.GetRecentPosts(
                master,
                p ?? 1,
                master.PageSize,
                PublishedValueFallback,
                VariationContextAccessor);

            return GetPagedListView(master, listNodes[0], posts, count, p);

        }
    }
}
