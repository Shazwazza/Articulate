using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the blog archive by tags (or categories)
    /// </summary>
    public class ArticulateListController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var contentByTags = Umbraco.GetContentByTags(new BlogModel(model.Content.Parent), "ArticulateCategories");

            //create a blog model of the main page
            var blogModel = new BlogModel(model.Content.Parent);
            var tagListModel = new TagListModel(
                blogModel,
                "Categories",
                contentByTags);

            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }
    }
}