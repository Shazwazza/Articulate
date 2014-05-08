using System;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the blog post by tags/categories
    /// </summary>
    public class ArticulateArchiveController : RenderMvcController
    {
        /// <summary>
        /// Hijack for the ArticulateList property type (built in to umbraco)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public override ActionResult Index(RenderModel model)
        {
            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTags = Umbraco.GetContentByTags(rootPageModel, "ArticulateCategories");

            var tagListModel = new TagListModel(
                rootPageModel,
                "Categories",
                contentByTags);

            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }

        /// <summary>
        /// Used to render the tag listing (virtual node)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ActionResult Tags(RenderModel model)
        {
            var tagPage = model.Content as ArticulateVirtualPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTags = Umbraco.GetContentByTags(rootPageModel, "ArticulateTags");

            var tagListModel = new TagListModel(
                rootPageModel,
                tagPage.Name,
                contentByTags);

            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }

    }
}