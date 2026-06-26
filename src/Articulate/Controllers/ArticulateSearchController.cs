#nullable enable
using Articulate.Attributes;
using Articulate.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders search results
    /// </summary>
    [ArticulateDynamicRoute]
    public class ArticulateSearchController(
        ILogger<ArticulateSearchController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        IArticulateSearcher articulateSearcher,
        IOptions<ArticulateCommentsOptions> commentsOptions)
        : ListControllerBase(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider,
            publishedValueFallback, commentsOptions)
    {
        /// <summary>
        /// Used to render the search result listing (virtual node)
        /// </summary>
        /// <param name="term">
        /// The search term
        /// </param>
        /// <param name="indexName">
        /// The searcher name (optional)
        /// </param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Search(string? term, string? indexName = null, int? p = null)
        {
            indexName = SanitizeIndexName(indexName);

            if (CurrentPage is null)
            {
                logger.LogWarning("ArticulateSearchController.Search: CurrentPage is null, returning 404");
                return NotFound();
            }

            IPublishedContent currentPage = CurrentPage;
            var masterModel = new MasterModel(currentPage, PublishedValueFallback, CommentsOptions);

            if (term is null)
            {
                return View("List", CreateEmptyListModel(currentPage, masterModel));
            }

            if (p is 1)
            {
                return Redirect(BuildSearchUrl(currentPage, term, indexName));
            }

            var pageNumber = p is > 0 ? p.Value : 1;

            IEnumerable<IPublishedContent>? searchResult = articulateSearcher.Search(
                term,
                indexName,
                masterModel.BlogArchiveNode.Id,
                masterModel.PageSize,
                pageNumber - 1,
                out var totalPosts);

            return GetPagedListView(masterModel, currentPage, searchResult ?? [], totalPosts, pageNumber);
        }

        private string? SanitizeIndexName(string? indexName)
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                return indexName;
            }

            if (!indexName.Equals(
                    Umbraco.Cms.Core.Constants.UmbracoIndexes.InternalIndexName,
                    StringComparison.OrdinalIgnoreCase) &&
                !indexName.Equals(
                    Umbraco.Cms.Core.Constants.UmbracoIndexes.MembersIndexName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return indexName;
            }

            logger.LogWarning(
                "ArticulateSearchController.Search: Blocked access to sensitive index '{IndexName}'",
                indexName);

            return Umbraco.Cms.Core.Constants.UmbracoIndexes.ExternalIndexName;
        }

        private ListModel CreateEmptyListModel(IPublishedContent currentPage, MasterModel masterModel) =>
            new(
                currentPage,
                new PagerModel(masterModel.PageSize, 0, 0),
                [],
                PublishedValueFallback,
                CommentsOptions);

        private string BuildSearchUrl(IPublishedContent currentPage, string term, string? indexName)
        {
            var url = currentPage.Url(PublishedUrlProvider);

            if (!string.IsNullOrWhiteSpace(term))
            {
                url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(url, "term", term);
            }

            if (!string.IsNullOrWhiteSpace(indexName))
            {
                url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(url, "indexName", indexName);
            }

            return url;
        }
    }
}
