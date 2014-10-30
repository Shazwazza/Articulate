using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Mvc;
using System.Web.Routing;
using Argotic.Common;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the blog post archive by tags/categories and also the tag/category blog listing
    /// </summary>
    /// <remarks>
    /// Cached for one minute
    /// </remarks>
    [OutputCache(Duration = 60, VaryByHeader = "host")]
    public class ArticulateTagsController : RenderMvcController
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

            var contentByTag = Umbraco.GetContentByTag(
                rootPageModel,
                tagPage.Name,
                tagGroup,
                baseUrl);

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

            if (p == null || p.Value <= 0)
            {
                p = 1;
            }

            //TODO: I wonder about the performance of this - when we end up with thousands of blog posts, 
            // this will probably not be so efficient. I wonder if using an XPath lookup for batches of children
            // would work? The children count could be cached. I'd rather not put blog posts under 'month' nodes
            // just for the sake of performance. Hrm.... Examine possibly too.

            var totalPosts = contentByTag.PostCount;
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
                p > 1 ? model.Content.Url.EnsureEndsWith('?') + "p=" + (p - 1) : null);

            var listModel = new ListModel(tagPage, contentByTag.Posts, pager);

            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }
    }
}