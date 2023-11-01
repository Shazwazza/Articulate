using System;
using System.Collections.Generic;
using System.Linq;
using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Website.ActionResults;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    public class ArticulateAuthorController : ListControllerBase
    {
        private readonly UmbracoHelper _umbracoHelper;

        public ArticulateAuthorController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedUrlProvider publishedUrlProvider,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            UmbracoHelper umbracoHelper)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider, publishedValueFallback, variationContextAccessor)
        {
            _umbracoHelper = umbracoHelper;
        }

        /// <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [NonAction]
        public override IActionResult Index() => Index(0);

        public IActionResult Index(int? p)
        {
            //create a master model
            var masterModel = new MasterModel(CurrentPage, PublishedValueFallback, VariationContextAccessor);

            var listNodes = masterModel.RootBlogNode.ChildrenOfType(ArticulateConstants.ArticulateArchiveContentTypeAlias).ToArray();
            if (listNodes.Length == 0)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            var totalPosts = _umbracoHelper.GetPostCount(CurrentPage.Name, listNodes.Select(x => x.Id).ToArray());

            if (!GetPagerModel(masterModel, totalPosts, p, out var pager))
            {
                return new RedirectToUmbracoPageResult(
                    CurrentPage.Parent,
                    PublishedUrlProvider,                    
                    UmbracoContextAccessor);
            }

            IEnumerable<IPublishedContent> authorPosts = _umbracoHelper.GetContentByAuthor(
                listNodes,
                CurrentPage.Name,
                pager,
                PublishedValueFallback,
                VariationContextAccessor);

            var author = new AuthorModel(
                CurrentPage,
                authorPosts,
                pager,
                totalPosts,
                PublishedValueFallback,
                VariationContextAccessor);
            
            return View(PathHelper.GetThemeViewPath(author, "Author"), author);
        }

        
    }
}
