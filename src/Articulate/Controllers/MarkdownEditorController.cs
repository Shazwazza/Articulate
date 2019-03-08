using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web;
using Umbraco.Web.Editors;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class MarkdownEditorController : PluginController
    {
        [System.Web.Mvc.HttpGet]
        public ActionResult NewPost(ContentModel model)
        {
            var vm = new MarkdownEditorInitModel
            {
                ArticulateNodeId = model.Content.Id,
                PostUrl = Url.GetUmbracoApiService<MardownEditorApiController>(controller => controller.PostNew()),
                IsAuthUrl = Url.GetUmbracoApiService<AuthenticationController>(controller => controller.IsAuthenticated()),
                DoAuthUrl = Url.GetUmbracoApiService<AuthenticationController>(controller => controller.PostLogin(null))
            };
            
            return View("~/App_Plugins/Articulate/Views/MarkdownEditor.cshtml", vm);
        }
    }
}