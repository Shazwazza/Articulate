using System;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the blog post archive by tags/categories
    /// </summary>
    public class ArticulateArchiveController : RenderMvcController
    {
        /// <summary>
        /// Used to render the category listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ActionResult Categories(RenderModel model)
        {
            return RenderTagsOrCategories(model, "ArticulateCategories", "categories");
        }

        /// <summary>
        /// Used to render the tag listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ActionResult Tags(RenderModel model)
        {
            return RenderTagsOrCategories(model, "ArticulateTags", "tags");
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
                contentByTags);

            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }

    }
}