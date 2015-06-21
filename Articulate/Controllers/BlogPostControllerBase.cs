using System.Web.Mvc;
using Articulate.Models;
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
