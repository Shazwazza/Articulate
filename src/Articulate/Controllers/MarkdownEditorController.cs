using System.Web.Mvc;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class MarkdownEditorController : PluginController
    {
        [System.Web.Mvc.HttpGet]
        public ActionResult NewPost(ContentModel model)
        {
            ViewBag.articulateNodeId = model.Content.Id;
            return View("~/App_Plugins/Articulate/Views/MarkdownEditor.cshtml");
        }
    }
}