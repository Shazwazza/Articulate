using System;
using System.Web.Mvc;
using System.Web.Routing;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the blog post archive by tags/categories and also the tag/category blog listing
    /// </summary>
    /// <remarks>
    /// Cached for one minute
    /// </remarks>
#if !DEBUG
    [OutputCache(Duration = 60, VaryByHeader = "host")]
#endif    
    public class ArticulateTagsController : ListControllerBase
    {
        /// <summary>
        /// Sets a custom action invoker so that the correct action is executed based on the specified tag/category url defined on the articulate root
        /// </summary>
        /// <param name="requestContext">The HTTP context and route data.</param>
        protected override void Initialize(RequestContext requestContext)
        {
            ActionInvoker = new TagsControllerActionInvoker();
            base.Initialize(requestContext);
        }

        /// <summary>
        /// Used to render the category listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="tag">The category to display if supplied</param>
        /// <param name="p"></param>
        /// <returns></returns>
        public ActionResult Categories(RenderModel model, string tag, int? p)
        {
            var caturlName = model.Content.GetPropertyValue<string>("categoriesUrlName");

            return tag.IsNullOrWhiteSpace()
                ? RenderTagsOrCategories(model, "ArticulateCategories", caturlName)
                : RenderByTagOrCategory(model, p, "ArticulateCategories", caturlName);
        }

        /// <summary>
        /// Used to render the tag listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="tag">The tag to display if supplied</param>
        /// <param name="p"></param>
        /// <returns></returns>
        public ActionResult Tags(RenderModel model, string tag, int? p)
        {
            var tagurlName = model.Content.GetPropertyValue<string>("tagsUrlName");

            return tag.IsNullOrWhiteSpace()
                ? RenderTagsOrCategories(model, "ArticulateTags", tagurlName) 
                : RenderByTagOrCategory(model, p, "ArticulateTags", tagurlName);
        }

        public ActionResult RenderTagsOrCategories(RenderModel model, string tagGroup, string baseUrl)
        {
            var tagPage = model.Content as ArticulateVirtualPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTags = Umbraco.GetContentByTags(rootPageModel, tagGroup, baseUrl);

            var tagListModel = new TagListModel(
                rootPageModel,
                tagPage.Name,
                rootPageModel.PageSize,
                new PostTagCollection(contentByTags));

            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }

        private ActionResult RenderByTagOrCategory(RenderModel model, int? p, string tagGroup, string baseUrl)
        {
            var tagPage = model.Content as ArticulateVirtualPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTag = Umbraco.GetContentByTag(rootPageModel, tagPage.Name, tagGroup, baseUrl);

            //this is a special case in the event that a tag contains a '.', when this happens we change it to a '-' 
            // when generating the URL. So if the above doesn't return any tags and the tag contains a '-', then we
            // will replace them with '.' and do the lookup again
            if (contentByTag == null && tagPage.Name.Contains("-"))
            {
                contentByTag = Umbraco.GetContentByTag(
                    rootPageModel,
                    tagPage.Name.Replace('-', '.'),
                    tagGroup,
                    baseUrl);
            }

            if (contentByTag == null)
            {
                return new HttpNotFoundResult();
            }

            return GetPagedListView(model, tagPage, contentByTag.Posts, contentByTag.PostCount, p); 
            
        }
    }
}