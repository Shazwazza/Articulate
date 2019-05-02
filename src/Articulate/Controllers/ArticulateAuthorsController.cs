using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// This is used to redirect the Authors node to the root so no 404s occur
    /// </summary>
    public class ArticulateAuthorsController : RenderMvcController
    {

        public override ActionResult Index(ContentModel model)
        {
            var root = new MasterModel(model.Content);

            //TODO: Should we have another setting for authors?
            if (root.RootBlogNode.Value<bool>("redirectArchive"))
            {
                return RedirectPermanent(root.RootBlogNode.Url);
            }

            //default

            var action = ControllerContext.RouteData.Values["action"].ToString();
            if (!EnsurePhsyicalViewExists(action))
                return new UmbracoNotFoundResult();

            return View(action, model);
        }
    }
}
