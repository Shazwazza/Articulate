using Articulate.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Models;
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
        private IArticulateSearcher _articulateSearcher;

        public ArticulateSearchController()
        {
        }

        public ArticulateSearchController(UmbracoContext umbracoContext, UmbracoHelper umbracoHelper, IArticulateSearcher articulateSearcher) : base(umbracoContext, umbracoHelper)
        {
            if (articulateSearcher == null) throw new ArgumentNullException(nameof(articulateSearcher));
            _articulateSearcher = articulateSearcher;
        }

        public ArticulateSearchController(UmbracoContext umbracoContext, IArticulateSearcher articulateSearcher) : base(umbracoContext)
        {
            if (articulateSearcher == null) throw new ArgumentNullException(nameof(articulateSearcher));
            _articulateSearcher = articulateSearcher;
        }

        protected IArticulateSearcher ArticulateSearcher => _articulateSearcher ?? (_articulateSearcher = new DefaultArticulateSearcher(Umbraco));

        /// <summary>
        /// Used to render the search result listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="term">
        /// The search term
        /// </param>
        /// <param name="provider">
        /// The search provider name (optional)
        /// </param>
        /// <param name="p"></param>
        /// <returns></returns>
        public ActionResult Search(RenderModel model, string term, string provider = null, int? p = null)
        {
            var searchPage = model.Content as ArticulateVirtualPage;
            if (searchPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new MasterModel(model.Content.Parent);

            if (term == null)
            {
                //nothing to search, just render the view
                var emptyList = new ListModel(searchPage, Enumerable.Empty<IPublishedContent>(), new PagerModel(rootPageModel.PageSize, 0, 0));
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

            int totalPosts;
            var searchResult = ArticulateSearcher.Search(term, provider, rootPageModel.BlogArchiveNode.Id, rootPageModel.PageSize, p.Value - 1, out totalPosts);

            return GetPagedListView(model, searchPage, searchResult, totalPosts, p);
        }
    }
}