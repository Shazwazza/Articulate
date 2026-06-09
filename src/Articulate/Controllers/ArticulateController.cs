#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the Articulate root node as the main blog post list by date
    /// </summary>
    public class ArticulateController(
        ILogger<ArticulateController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        UmbracoHelper umbracoHelper)
        : ListControllerBase(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider,
            publishedValueFallback)
    {
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

            logger.LogWarning("ArticulateController.Index: CurrentPage is null, returning 404");
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
            IEnumerable<IPublishedContent> archiveNodes =
                model.Content.Children().Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateArchive);
            IPublishedContent[] listNodes = archiveNodes.ToArray();

            if (listNodes.Length == 0)
            {
                logger.LogWarning("Articulate root {NodeId} has no published ArticulateArchive children", CurrentPage?.Id);
                return NotFound();
            }

            var master = new MasterModel(model.Content, PublishedValueFallback);

            PagerModel pager = CreateRequestedPager(master, p);

            (int totalPosts, IPublishedContent[] posts) = umbracoHelper.GetPagedPostsSortedByPublishedDate(
                pager,
                null,
                [.. listNodes.Select(x => x.Id)]);

            return GetPagedListView(master, listNodes[0], posts, totalPosts, p);
        }
    }
}
