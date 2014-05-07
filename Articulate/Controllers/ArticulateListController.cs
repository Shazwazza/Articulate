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
            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTags = Umbraco.GetContentByTags(rootPageModel, "ArticulateCategories");

            var tagListModel = new TagListModel(
                rootPageModel,
                "Categories",
                contentByTags);

            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }
    }
}