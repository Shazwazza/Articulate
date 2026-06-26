#nullable enable
using Articulate.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the Articulate Archive node as a blog post list by date
    /// </summary>
    public class ArticulateArchiveController(
        ILogger<ArticulateArchiveController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        UmbracoHelper umbraco,
        IOptions<ArticulateCommentsOptions> commentsOptions)
        : ListControllerBase(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider,
            publishedValueFallback, commentsOptions)
    {
        private UmbracoHelper Umbraco { get; } = umbraco;

        /// <summary>
        /// Declare new Index action with optional page number
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Index(int? p)
        {
            if (CurrentPage is not null)
            {
                return RenderView(new ContentModel(CurrentPage), p);
            }

            logger.LogWarning("ArticulateArchiveController.Index: CurrentPage is null, returning 404");
            return NotFound();
        }

        /// <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <returns></returns>
        [NonAction]
        public override IActionResult Index() => Index(0);

        private IActionResult RenderView(ContentModel model, int? p = null)
        {
            var archive = new MasterModel(model.Content, PublishedValueFallback, CommentsOptions);

            // redirect to root node when "redirectArchive" is configured
            if (archive.RootBlogNode.Value<bool>("redirectArchive"))
            {
                return RedirectPermanent(archive.RootBlogNode.Url());
            }

            PagerModel pager = CreateRequestedPager(archive, p);
            (var totalPosts, IPublishedContent[] posts) = Umbraco.GetPagedPostsSortedByPublishedDate(pager, null, archive.Id);

            return GetPagedListView(archive, archive, posts, totalPosts, p);
        }
    }
}
