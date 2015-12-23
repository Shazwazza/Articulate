﻿using System;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Articulate.Plugins;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders search results
    /// </summary>
    public class ArticulateSearchController : RenderMvcController
    {
        private readonly IArticulateSearchPlugin _articulateSearchPlugin;

        public ArticulateSearchController(IArticulateSearchPlugin articulateSearchPlugin)
        {
            _articulateSearchPlugin = articulateSearchPlugin ?? new ArticulateSearch();
        }

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
            var tagPage = model.Content as ArticulateVirtualPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            if (term == null)
            {
                //nothing to search, just render the view
                var emptyList = new ListModel(tagPage, Enumerable.Empty<IPublishedContent>(), new PagerModel(rootPageModel.PageSize, 0, 0));
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

            var searchResult = _articulateSearchPlugin.Search(term, provider, rootPageModel.BlogArchiveNode.Id);

            //TODO: I wonder about the performance of this - when we end up with thousands of blog posts, 
            // this will probably not be so efficient. I wonder if using an XPath lookup for batches of children
            // would work? The children count could be cached. I'd rather not put blog posts under 'month' nodes
            // just for the sake of performance. Hrm.... Examine possibly too.

            var totalPosts = searchResult.Count();
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
                totalPages > p ? model.Content.Url.EnsureEndsWith('?') + "term=" + term + "&p=" + (p + 1) : null,
                p > 2 ? model.Content.Url.EnsureEndsWith('?') + "term=" + term + "&p=" + (p - 1) : p > 1 ? model.Content.Url.EnsureEndsWith('?') + "term=" + term : null);

            var listModel = new ListModel(tagPage, searchResult, pager);

            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }
    }
}