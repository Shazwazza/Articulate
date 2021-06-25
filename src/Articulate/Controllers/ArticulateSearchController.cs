using Articulate.Models;
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.Common.Controllers;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Web.Website.ActionResults;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders search results
    /// </summary>
    public class ArticulateSearchController : ListControllerBase
    {
        private readonly IArticulateSearcher _articulateSearcher;

        public ArticulateSearchController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedUrlProvider publishedUrlProvider,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            IImageUrlGenerator imageUrlGenerator,
            IArticulateSearcher articulateSearcher)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider, publishedValueFallback, variationContextAccessor, imageUrlGenerator)
        {
            _articulateSearcher = articulateSearcher;
        }

        /// <summary>
        /// Used to render the search result listing (virtual node)
        /// </summary>
        /// <param name="term">
        /// The search term
        /// </param>
        /// <param name="provider">
        /// The searcher name (optional)
        /// </param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Search(string term, string provider = null, int? p = null)
        {
            if (CurrentPage is not ArticulateVirtualPage searchPage)
            {
                throw new InvalidOperationException("The ContentModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a master model
            var masterModel = new MasterModel(CurrentPage, PublishedValueFallback, VariationContextAccessor);

            if (term == null)
            {
                //nothing to search, just render the view
                var emptyList = new ListModel(
                    searchPage,
                    new PagerModel(masterModel.PageSize, 0, 0),
                    Enumerable.Empty<IPublishedContent>(),
                    PublishedValueFallback,
                    VariationContextAccessor,
                    ImageUrlGenerator);

                return View(PathHelper.GetThemeViewPath(emptyList, "List"), emptyList);
            }

            if (p != null && p.Value == 1)
            {
                return new RedirectToUmbracoPageResult(
                    CurrentPage,
                    PublishedUrlProvider,
                    UmbracoContextAccessor);
            }

            if (p == null || p.Value <= 0)
            {
                p = 1;
            }

            var searchResult = _articulateSearcher.Search(term, provider, masterModel.BlogArchiveNode.Id, masterModel.PageSize, p.Value - 1, out var totalPosts);

            return GetPagedListView(masterModel, searchPage, searchResult, totalPosts, p);
        }
    }
}
