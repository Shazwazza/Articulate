using System;
using System.Collections.Generic;
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
    /// Renders the Articulate Archive node as a blog post list by date
    /// </summary>
    public class ArticulateArchiveController : ListControllerBase
    {
        public ArticulateArchiveController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedUrlProvider publishedUrlProvider,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            UmbracoHelper umbraco)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider, publishedValueFallback, variationContextAccessor)
        {
            Umbraco = umbraco;
        }

        public UmbracoHelper Umbraco { get; }

        /// <summary>
        /// Declare new Index action with optional page number
        /// </summary>
        /// <param name="model"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Index(int? p) => RenderView(new ContentModel(CurrentPage), p);

        // <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [NonAction]
        public override IActionResult Index() => Index(0);

        private IActionResult RenderView(IContentModel model, int? p = null)
        {
            var archive = new MasterModel(model.Content, PublishedValueFallback, VariationContextAccessor);

            // redirect to root node when "redirectArchive" is configured
            if (archive.RootBlogNode.Value<bool>("redirectArchive"))
            {
                return RedirectPermanent(archive.RootBlogNode.Url());
            }

            //Get post count by xpath is much faster than iterating all children to get a count
            var count = Umbraco.GetPostCount(archive.Id);

            if (!int.TryParse(archive.RootBlogNode.Value<string>("pageSize"), out int pageSize))
            {
                pageSize = 10;
            }

            IEnumerable<PostModel> posts = Umbraco.GetRecentPostsByArchive(
                archive,
                1,
                pageSize,
                PublishedValueFallback,
                VariationContextAccessor);

            return GetPagedListView(archive, archive, posts, count, null);          
        }
    }
}
