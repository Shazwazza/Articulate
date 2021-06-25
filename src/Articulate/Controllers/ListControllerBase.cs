using System;
using System.Collections.Generic;
using System.Text;
using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Website.ActionResults;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Media;
using Umbraco.Extensions;
using Microsoft.Extensions.Primitives;

namespace Articulate.Controllers
{
    /// <summary>
    /// Base controller providing common functionality for listing pages
    /// </summary>
    public abstract class ListControllerBase : RenderController
    {
        protected ListControllerBase(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedUrlProvider publishedUrlProvider,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            IImageUrlGenerator imageUrlGenerator)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            UmbracoContextAccessor = umbracoContextAccessor;
            PublishedUrlProvider = publishedUrlProvider;
            PublishedValueFallback = publishedValueFallback;
            VariationContextAccessor = variationContextAccessor;
            ImageUrlGenerator = imageUrlGenerator;
        }

        public IUmbracoContextAccessor UmbracoContextAccessor { get; }
        public IPublishedUrlProvider PublishedUrlProvider { get; }
        public IPublishedValueFallback PublishedValueFallback { get; }
        public IVariationContextAccessor VariationContextAccessor { get; }
        public IImageUrlGenerator ImageUrlGenerator { get; }

        /// <summary>
        /// Gets a paged list view for a given posts by author/tags/categories model
        /// </summary>
        protected IActionResult GetPagedListView(IMasterModel masterModel, IPublishedContent pageNode, IEnumerable<IPublishedContent> listItems, long totalPosts, int? p)
        {
            if (masterModel == null) throw new ArgumentNullException(nameof(masterModel));
            if (pageNode == null) throw new ArgumentNullException(nameof(pageNode));
            if (listItems == null) throw new ArgumentNullException(nameof(listItems));

            if (!GetPagerModel(masterModel, totalPosts, p, out var pager))
            {
                return new RedirectToUmbracoPageResult(
                    masterModel.RootBlogNode,
                    PublishedUrlProvider,
                    UmbracoContextAccessor);
            }

            var listModel = new ListModel(pageNode, pager, listItems, PublishedValueFallback, VariationContextAccessor, ImageUrlGenerator);

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
            foreach (var key in Request.Query.Keys)
            {
                if (key == "p") continue;
                if (Request.Query.TryGetValue(key, out StringValues val))
                {
                    foreach (var v in val)
                    {
                        queryStrings.Append($"&{key}={v}");
                    }
                }
            }

            pager = new PagerModel(
                pageSize,
                p.Value - 1,
                totalPages,
                totalPages > p
                    ? GetPagedUrl(masterModel.Url(), (p + 1), queryStrings.ToString())
                    : null,
                p > 2
                    ? GetPagedUrl(masterModel.Url(), (p - 1), queryStrings.ToString())
                    : p > 1
                        ? GetPagedUrl(masterModel.Url(), null, queryStrings.ToString())
                        : null);

            return true;
        }

        private string GetPagedUrl(string baseUrl, int? page, string queryStrings)
            => page.HasValue
                ? $"{baseUrl.EnsureEndsWith('?')}p={page}{queryStrings}"
                : $"{baseUrl.EnsureEndsWith('?')}{queryStrings.TrimStart('&')}";
    }
}
