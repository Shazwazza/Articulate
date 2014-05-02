using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class ArticulateListController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var post = new ListModel(model.Content);
            return View(PathHelper.GetThemeViewPath(post, "List"), post);
        }
    }
}