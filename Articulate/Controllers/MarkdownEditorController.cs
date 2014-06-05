using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using Articulate.Models;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.IO;
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
        public async Task<HttpResponseMessage> PostNew()
        {
            //TODO: Delete unused files!

            if (!Request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            var str = IOHelper.MapPath("~/App_Data/TEMP/FileUploads");
            Directory.CreateDirectory(str);
            var provider = new MultipartFormDataStreamProvider(str);

            var multiPartRequest = await Request.Content.ReadAsMultipartAsync(provider);
            
            if (multiPartRequest.FormData["model"] == null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "The request was not formatted correctly and is missing the 'model' parameter"));
            }

            var model = JsonConvert.DeserializeObject<MardownEditorModel>(multiPartRequest.FormData["model"]);

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

            //parse out the images, we may be posting more than is in the body            
            model.Body = Regex.Replace(model.Body, @"\[i:(\d+)\:(.*?)]", m =>
            {
                var index = m.Groups[1].Value.TryConvertTo<int>();
                if (index)
                {
                    //get the file at this index
                    var file = multiPartRequest.FileData[index.Result];

                    var rndId = Guid.NewGuid().ToString("N");

                    using (var stream = File.OpenRead(file.LocalFileName))
                    {
                        var savedFile = UmbracoMediaFile.Save(stream, "articulate/" + rndId + "/" +
                            file.Headers.ContentDisposition.FileName.TrimStart("\"").TrimEnd("\""));

                        var result = string.Format("![{0}]({1})",
                            savedFile.Url,
                            savedFile.Url
                            );

                        return result;
                    }
                }

                return m.Value;
            });

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