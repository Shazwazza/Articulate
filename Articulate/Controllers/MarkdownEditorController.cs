using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class MarkdownEditorController : UmbracoController
    {
        public ActionResult NewPost()
        {
            return View("~/App_Plugins/Articulate/Views/MarkdownEditor.cshtml");
        }
    }
}