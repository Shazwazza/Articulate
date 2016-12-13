using Articulate.Models;
using System.Web.Mvc;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public abstract class BlogPostControllerBase : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var post = new PostModel(model.Content);
            return View(PathHelper.GetThemeViewPath(post, "Post"), post);
        }
    }
}