using Articulate.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders search results
    /// </summary>
    public class ArticulateSearchController : ListControllerBase
    {
        public ArticulateSearchController(IGlobalSettings globalSettings, UmbracoContext umbracoContext, ServiceContext services, AppCaches appCaches, IProfilingLogger profilingLogger, UmbracoHelper umbracoHelper, IArticulateSearcher articulateSearcher) : base(globalSettings, umbracoContext, services, appCaches, profilingLogger, umbracoHelper)
        {
            ArticulateSearcher = articulateSearcher;
        }

        private IArticulateSearcher ArticulateSearcher { get; }

        /// <summary>
        /// Used to render the search result listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="term">
        /// The search term
        /// </param>
        /// <param name="provider">
        /// The searcher name (optional)
        /// </param>
        /// <param name="p"></param>
        /// <returns></returns>
        public ActionResult Search(ContentModel model, string term, string provider = null, int? p = null)
        {
            var searchPage = model.Content as ArticulateVirtualPage;
            if (searchPage == null)
            {
                throw new InvalidOperationException("The ContentModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a master model
            var masterModel = new MasterModel(model.Content);

            if (term == null)
            {
                //nothing to search, just render the view
                var emptyList = new ListModel(searchPage, Enumerable.Empty<IPublishedContent>(), new PagerModel(masterModel.PageSize, 0, 0));
                return View(PathHelper.GetThemeViewPath(emptyList, "List"), emptyList);
            }

            if (p != null && p.Value == 1)
            {
                return new RedirectToUmbracoPageResult(model.Content, UmbracoContext);
            }

            if (p == null || p.Value <= 0)
            {
                p = 1;
            }

            var searchResult = ArticulateSearcher.Search(term, provider, masterModel.BlogArchiveNode.Id, masterModel.PageSize, p.Value - 1, out var totalPosts);

            return GetPagedListView(masterModel, searchPage, searchResult, totalPosts, p);
        }
    }
}