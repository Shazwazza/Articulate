using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class ArticulateAuthorController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {            
            var author = new AuthorModel(model.Content, Umbraco);
            return View(PathHelper.GetThemeViewPath(author, "Author"), author);
        }
    }
}