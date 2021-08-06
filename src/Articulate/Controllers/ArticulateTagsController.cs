using Articulate.Models;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.Common.Controllers;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Media;
using Umbraco.Extensions;
using Umbraco.Cms.Web.Common;
using Articulate.Services;
using Umbraco.Cms.Core.PublishedCache;
using System.Collections.Generic;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the blog post archive by tags/categories and also the tag/category blog listing
    /// </summary>
    /// <remarks>
    /// Cached for one minute
    /// </remarks>
#if !DEBUG
    // TODO: This won't work
    //[OutputCache(Duration = 60, VaryByHeader = "host")]
#endif

    public class ArticulateTagsController : ListControllerBase
    {
        private readonly UmbracoHelper _umbracoHelper;
        private readonly ArticulateTagService _articulateTagService;
        private readonly ITagQuery _tagQuery;

        public ArticulateTagsController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedUrlProvider publishedUrlProvider,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            IImageUrlGenerator imageUrlGenerator,
            UmbracoHelper umbracoHelper,
            ArticulateTagService articulateTagService,
            ITagQuery tagQuery)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider, publishedValueFallback, variationContextAccessor, imageUrlGenerator)
        {
            _umbracoHelper = umbracoHelper;
            _articulateTagService = articulateTagService;
            _tagQuery = tagQuery;
        }

        // TODO: This won't work anymore:

        ///// <summary>
        ///// Sets a custom action invoker so that the correct action is executed based on the specified tag/category url defined on the articulate root
        ///// </summary>
        ///// <param name="requestContext">The HTTP context and route data.</param>
        //protected override void Initialize(RequestContext requestContext)
        //{
        //    ActionInvoker = new TagsControllerActionInvoker();
        //    base.Initialize(requestContext);
        //}

        /// <summary>
        /// Used to render the category listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="tag">The category to display if supplied</param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Categories(string tag, int? p)
        {
            var caturlName = CurrentPage.Value<string>("categoriesUrlName");

            return tag.IsNullOrWhiteSpace()
                ? RenderTagsOrCategories("ArticulateCategories", caturlName)
                : RenderByTagOrCategory(p, "ArticulateCategories", caturlName);
        }

        /// <summary>
        /// Used to render the tag listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="tag">The tag to display if supplied</param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Tags(string tag, int? p)
        {
            var tagurlName = CurrentPage.Value<string>("tagsUrlName");

            return tag.IsNullOrWhiteSpace()
                ? RenderTagsOrCategories("ArticulateTags", tagurlName)
                : RenderByTagOrCategory(p, "ArticulateTags", tagurlName);
        }

        private IActionResult RenderTagsOrCategories(string tagGroup, string baseUrl)
        {
            if (CurrentPage is not ArticulateVirtualPage tagPage)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new MasterModel(CurrentPage.Parent, PublishedValueFallback, VariationContextAccessor);

            IEnumerable<PostsByTagModel> contentByTags = _articulateTagService.GetContentByTags(
                _umbracoHelper,
                _tagQuery,
                rootPageModel,
                tagGroup,
                baseUrl);

            var tagListModel = new TagListModel(
                rootPageModel,
                tagPage.Name,
                rootPageModel.PageSize,
                new PostTagCollection(contentByTags),
                PublishedValueFallback,
                VariationContextAccessor);

            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }

        private IActionResult RenderByTagOrCategory(int? p, string tagGroup, string baseUrl)
        {
            if (CurrentPage is not ArticulateVirtualPage tagPage)
            {
                throw new InvalidOperationException("The ContentModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a master model
            var masterModel = new MasterModel(CurrentPage, PublishedValueFallback, VariationContextAccessor);

            PostsByTagModel contentByTag = _articulateTagService.GetContentByTag(
                _umbracoHelper,
                masterModel,
                tagPage.Name,
                tagGroup,
                baseUrl,
                p ?? 1,
                masterModel.PageSize);

            //this is a special case in the event that a tag contains a '.', when this happens we change it to a '-'
            // when generating the URL. So if the above doesn't return any tags and the tag contains a '-', then we
            // will replace them with '.' and do the lookup again
            if ((contentByTag == null || contentByTag.PostCount == 0) && tagPage.Name.Contains("-"))
            {
                contentByTag = _articulateTagService.GetContentByTag(
                    _umbracoHelper,
                    masterModel,
                    tagPage.Name.Replace('-', '.'),
                    tagGroup,
                    baseUrl,
                    p ?? 1, masterModel.PageSize);
            }

            if (contentByTag == null)
            {
                return new NotFoundResult();
            }

            return GetPagedListView(masterModel, tagPage, contentByTag.Posts, contentByTag.PostCount, p);
        }
    }
}
