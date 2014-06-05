using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class MarkdownEditorController : UmbracoController
    {
        [System.Web.Mvc.HttpGet]
        public ActionResult NewPost()
        {
            ViewBag.articulateNodeId = RouteData.Values["id"];
            return View("~/App_Plugins/Articulate/Views/MarkdownEditor.cshtml");
        }
    }
}