using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class ArticulateTagsController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var post = new ListModel(model.Content);
            return View(PathHelper.GetThemeViewPath(post, "List"), post);
        }

        public ActionResult Tag(RenderModel model)
        {
            var post = new ListModel(model.Content);
            return View(PathHelper.GetThemeViewPath(post, "List"), post);
        }
    }
}