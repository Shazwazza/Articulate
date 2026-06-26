#nullable enable
using Articulate.Attributes;
using Articulate.Options;
using Articulate.Routing;
using Articulate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the blog post archive by tags/categories and also the tag/category blog listing
    /// </summary>
    /// <remarks>
    /// Cached for one minute
    /// </remarks>
    [OutputCache(PolicyName = "Articulate60")]
    [ArticulateDynamicRoute]
    public class ArticulateTagsController(
        ILogger<ArticulateTagsController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider publishedUrlProvider,
        IPublishedValueFallback publishedValueFallback,
        UmbracoHelper umbracoHelper,
        ArticulateTagService articulateTagService,
        ITagQuery tagQuery,
        IOptions<ArticulateCommentsOptions> commentsOptions)
        : ListControllerBase(logger, compositeViewEngine, umbracoContextAccessor, publishedUrlProvider,
            publishedValueFallback, commentsOptions)
    {
        /// <summary>
        /// Used to render the category listing (virtual node)
        /// </summary>
        /// <param name="tag">The category to display if supplied</param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Categories(string tag, int? p)
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("ArticulateTagsController.Categories: CurrentPage is null, returning 404");
                return NotFound();
            }

            string? categoriesUrlName = ArticulateRouteSegmentHelper.GetConfiguredSegment(CurrentPage, "categoriesUrlName");
            if (categoriesUrlName is null)
            {
                return NotFound();
            }

            return tag.IsNullOrWhiteSpace()
                ? RenderTagsOrCategories(ArticulateConstants.DataType.ArticulateCategories, categoriesUrlName)
                : RenderByTagOrCategory(tag, p, ArticulateConstants.DataType.ArticulateCategories, categoriesUrlName);
        }

        /// <summary>
        /// Used to render the tag listing (virtual node)
        /// </summary>
        /// <param name="tag">The tag to display if supplied</param>
        /// <param name="p"></param>
        /// <returns></returns>
        public IActionResult Tags(string tag, int? p)
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("ArticulateTagsController.Tags: CurrentPage is null, returning 404");
                return NotFound();
            }

            string? tagUrlName = ArticulateRouteSegmentHelper.GetConfiguredSegment(CurrentPage, "tagsUrlName");
            if (tagUrlName is null)
            {
                return NotFound();
            }

            return tag.IsNullOrWhiteSpace()
                ? RenderTagsOrCategories(ArticulateConstants.DataType.ArticulateTags, tagUrlName)
                : RenderByTagOrCategory(tag, p, ArticulateConstants.DataType.ArticulateTags, tagUrlName);
        }

        private IActionResult RenderTagsOrCategories(string tagGroup, string baseUrl)
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("ArticulateTagsController.RenderTagsOrCategories: CurrentPage is null, returning 404");
                return NotFound();
            }

            // create a blog model of the main page
            var rootPageModel = new MasterModel(CurrentPage, PublishedValueFallback, CommentsOptions);

            IEnumerable<PostsByTagModel> contentByTags = articulateTagService.GetContentByTags(
                umbracoHelper,
                tagQuery,
                rootPageModel,
                tagGroup,
                baseUrl);

            var tagListModel = new TagListModel(
                rootPageModel,
                CurrentPage.Name,
                rootPageModel.PageSize,
                new PostTagCollection(contentByTags),
                PublishedValueFallback);

            return View("Tags", tagListModel);
        }

        private IActionResult RenderByTagOrCategory(string tag, int? p, string tagGroup, string baseUrl)
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("ArticulateTagsController.RenderByTagOrCategory: CurrentPage is null, returning 404");
                return NotFound();
            }

            // create a master model
            var masterModel = new MasterModel(CurrentPage, PublishedValueFallback, CommentsOptions);

            PostsByTagModel contentByTag = articulateTagService.GetContentByTag(
                umbracoHelper,
                masterModel,
                tag,
                tagGroup,
                baseUrl,
                p ?? 1,
                masterModel.PageSize);

            // this is a special case in the event that a tag contains a '.', when this happens we change it to a '-'
            // when generating the URL. So if the above doesn't return any tags and the tag contains a '-', then we
            // will replace them with '.' and do the lookup again
            if (contentByTag.PostCount == 0 && tag.Contains('-'))
            {
                contentByTag = articulateTagService.GetContentByTag(
                    umbracoHelper,
                    masterModel,
                    tag.Replace('-', '.'),
                    tagGroup,
                    baseUrl,
                    p ?? 1,
                    masterModel.PageSize);
            }

            return contentByTag is not { Posts: not null } ? NotFound() : GetPagedListView(masterModel, CurrentPage, contentByTag.Posts, contentByTag.PostCount, p);
        }
    }
}
