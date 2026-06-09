#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Website.ActionResults;

namespace Articulate.Controllers
{
    /// <summary>
    /// Controller for displaying author details and their posts.
    /// </summary>
    public class ArticulateAuthorController(
        ILogger<ArticulateAuthorController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        UmbracoHelper umbracoHelper)
        : ListControllerBase(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider,
            publishedValueFallback)
    {
        /// <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <returns></returns>
        [NonAction]
        public override IActionResult Index() => Index(0);

        /// <summary>
        /// Renders the author page and their posts with optional pagination.
        /// </summary>
        public IActionResult Index(int? p)
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("ArticulateAuthorController.Index: CurrentPage is null, returning 404");
                return NotFound();
            }

            // create a master model
            var masterModel = new MasterModel(CurrentPage, PublishedValueFallback);
            IEnumerable<IPublishedContent> archiveNodes = masterModel.RootBlogNode.Children()
                    .Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateArchive);
            IPublishedContent[] listNodes = archiveNodes.ToArray();
            if (listNodes.Length == 0)
            {
                logger.LogWarning(
                    "ArticulateAuthorController: No ArticulateArchive child nodes found for root {RootId} ('{RootName}') using theme '{Theme}' while rendering author page {PageId} ('{PageName}').",
                    masterModel.RootBlogNode.Id,
                    masterModel.RootBlogNode.Name,
                    masterModel.Theme,
                    CurrentPage.Id,
                    CurrentPage.Name);

                return NotFound();
            }

            PagerModel initialPager = CreateRequestedPager(masterModel, p);

            (int totalPosts, IPublishedContent[] posts) = umbracoHelper.GetPagedContentByAuthor(
                listNodes,
                CurrentPage.Name,
                initialPager);

            if (!GetPagerModel(masterModel, totalPosts, p, out PagerModel? pager) || pager is null)
            {
                return new RedirectToUmbracoPageResult(
                    CurrentPage.Parent(),
                    PublishedUrlProvider,
                    UmbracoContextAccessor);
            }

            var author = new AuthorModel(
                CurrentPage,
                posts,
                pager,
                totalPosts,
                posts.FirstOrDefault()?.Value<DateTime>("publishedDate"),
                PublishedValueFallback);

            return View("Author", author);
        }
    }
}
