using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Base controller providing common functionality for listing pages
    /// </summary>
    public abstract class ListControllerBase : RenderMvcController
    {
        protected ListControllerBase()
        {
        }

        protected ListControllerBase(UmbracoContext umbracoContext)
            : base(umbracoContext)
        {
        }

        protected ListControllerBase(UmbracoContext umbracoContext, UmbracoHelper umbracoHelper)
            : base(umbracoContext, umbracoHelper)
        {
        }

        /// <summary>
        /// Gets a paged list view for a given posts by author/tags/categories model
        /// </summary>
        protected ActionResult GetPagedListView(IRenderModel model, IPublishedContent publishedContent, int totalPosts, int? p)
        {
            return GetPagedListView(model, publishedContent, null, totalPosts, p);
        }

        /// <summary>
        /// Gets a paged list view for a given posts by author/tags/categories model
        /// </summary>
        protected ActionResult GetPagedListView(IRenderModel model, IPublishedContent publishedContent, IEnumerable<IPublishedContent> listItems, int totalPosts, int? p)
        {
            var rootPageModel = new ListModel(model.Content);

            if (p == null || p.Value <= 0)
            {
                p = 1;
            }

            //TODO: I wonder about the performance of this - when we end up with thousands of blog posts, 
            // this will probably not be so efficient. I wonder if using an XPath lookup for batches of children
            // would work? The children count could be cached. I'd rather not put blog posts under 'month' nodes
            // just for the sake of performance. Hrm.... Examine possibly too.

            var pageSize = rootPageModel.PageSize;
            var totalPages = totalPosts == 0 ? 1 : Convert.ToInt32(Math.Ceiling((double)totalPosts / pageSize));

            //Invalid page, redirect without pages
            if (totalPages < p)
            {
                return new RedirectToUmbracoPageResult(model.Content.Parent, UmbracoContext);
            }

            var pager = new PagerModel(
                pageSize,
                p.Value - 1,
                totalPages,
                totalPages > p ? model.Content.Url.EnsureEndsWith('?') + "p=" + (p + 1) : null,
                p > 2 ? model.Content.Url.EnsureEndsWith('?') + "p=" + (p - 1) : p > 1 ? model.Content.Url : null);
            
            var listModel = listItems != null
                ? new ListModel(publishedContent, listItems, pager)
                : new ListModel(publishedContent, pager);

            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }
    }
}