using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

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

    public class MardownEditorApiController : UmbracoAuthorizedApiController
    {
        public HttpResponseMessage PostNew(MardownEditorModel model)
        {
            if (model.ArticulateNodeId.HasValue == false)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No id specified"));
            }

            if (!ModelState.IsValid)
            {
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }

            var articulateNode = Services.ContentService.GetById(model.ArticulateNodeId.Value);
            if (articulateNode == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No Articulate node found with the specified id"));
            }
            var archive = Services.ContentService.GetChildren(model.ArticulateNodeId.Value)
                .FirstOrDefault(x => x.ContentType.Alias.InvariantEquals("ArticulateArchive"));
            if (archive == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No Articulate Archive node found for the specified id"));
            }

            var content = Services.ContentService.CreateContent(
                model.Title,
                archive.Id,
                "ArticulateMarkdown",
                Security.GetUserId());

            content.SetValue("markdown", model.Body);
            if (model.Excerpt.IsNullOrWhiteSpace() == false)
            {
                content.SetValue("excerpt", model.Excerpt);
            }
            //TODO: Fill in the rest
            var status = Services.ContentService.SaveAndPublishWithStatus(content, Security.GetUserId());
            if (status.Success == false)
            {
                ModelState.AddModelError("server", "Publishing failed");
                //TODO: need to say why!
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }

            var published = Umbraco.TypedContent(content.Id);

            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(published.UrlWithDomain(), Encoding.UTF8, "text/html");
            return response;
        }
    }
}