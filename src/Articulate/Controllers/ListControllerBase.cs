using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Mvc;
using Articulate.Models;
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
    /// Base controller providing common functionality for listing pages
    /// </summary>
    public abstract class ListControllerBase : RenderMvcController
    {
        protected ListControllerBase()
        {
        }

        protected ListControllerBase(IGlobalSettings globalSettings, UmbracoContext umbracoContext, ServiceContext services, AppCaches appCaches, IProfilingLogger profilingLogger, UmbracoHelper umbracoHelper) : base(globalSettings, umbracoContext, services, appCaches, profilingLogger, umbracoHelper)
        {
        }


        /// <summary>
        /// Gets a paged list view for a given posts by author/tags/categories model
        /// </summary>
        protected ActionResult GetPagedListView(IMasterModel masterModel, IPublishedContent pageNode, IEnumerable<IPublishedContent> listItems, long totalPosts, int? p)
        {
            if (masterModel == null) throw new ArgumentNullException(nameof(masterModel));
            if (pageNode == null) throw new ArgumentNullException(nameof(pageNode));
            if (listItems == null) throw new ArgumentNullException(nameof(listItems));

            if (!GetPagerModel(masterModel, totalPosts, p, out var pager))
            {
                return new RedirectToUmbracoPageResult(masterModel.RootBlogNode, UmbracoContext);
            }

            var listModel = new ListModel(pageNode, listItems, pager);

            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }

        protected bool GetPagerModel(IMasterModel masterModel, long totalPosts, int? p, out PagerModel pager)
        {
            if (p == null || p.Value <= 0)
            {
                p = 1;
            }

            var pageSize = masterModel.PageSize;
            var totalPages = totalPosts == 0 ? 1 : Convert.ToInt32(Math.Ceiling((double)totalPosts / pageSize));

            //Invalid page, redirect without pages
            if (totalPages < p)
            {
                pager = null;
                return false;
            }

            //maintain query strings
            var queryStrings = new StringBuilder();
            foreach (var key in Request.QueryString.AllKeys)
            {
                if (key == "p") continue;
                var val = Request.QueryString.GetValues(key);
                if (val == null) continue;
                foreach (var v in val)
                {
                    queryStrings.Append($"&{key}={v}");
                }
            }

            pager = new PagerModel(
                pageSize,
                p.Value - 1,
                totalPages,
                totalPages > p
                    ? GetPagedUrl(masterModel.Url, (p + 1), queryStrings.ToString())
                    : null,
                p > 2
                    ? GetPagedUrl(masterModel.Url, (p - 1), queryStrings.ToString())
                    : p > 1
                        ? GetPagedUrl(masterModel.Url, null, queryStrings.ToString())
                        : null);

            return true;
        }

        private string GetPagedUrl(string baseUrl, int? page, string queryStrings)
        {
            return page.HasValue
                ? $"{baseUrl.EnsureEndsWith('?')}p={page}{queryStrings}"
                : $"{baseUrl.EnsureEndsWith('?')}{queryStrings.TrimStart('&')}";
        }
    }
}