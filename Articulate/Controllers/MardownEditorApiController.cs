using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Compilation;
using System.Web.Http;
using Articulate.Models;
using Newtonsoft.Json;
using umbraco.BusinessLogic.Actions;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.WebApi;
using File = System.IO.File;

namespace Articulate.Controllers
{
    public class MardownEditorApiController : UmbracoAuthorizedApiController
    {
        public async Task<HttpResponseMessage> PostNew()
        {
            if (!Request.Content.IsMimeMultipartContent())
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            var str = IOHelper.MapPath("~/App_Data/TEMP/FileUploads");
            Directory.CreateDirectory(str);
            var provider = new MultipartFormDataStreamProvider(str);

            var multiPartRequest = await Request.Content.ReadAsMultipartAsync(provider);
            
            if (multiPartRequest.FormData["model"] == null)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "The request was not formatted correctly and is missing the 'model' parameter"));
            }

            var model = JsonConvert.DeserializeObject<MardownEditorModel>(multiPartRequest.FormData["model"]);

            if (model.ArticulateNodeId.HasValue == false)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No id specified"));
            }

            if (!ModelState.IsValid)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }
            
            var articulateNode = Services.ContentService.GetById(model.ArticulateNodeId.Value);
            if (articulateNode == null)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No Articulate node found with the specified id"));
            }
            var archive = Services.ContentService.GetChildren(model.ArticulateNodeId.Value)
                .FirstOrDefault(x => x.ContentType.Alias.InvariantEquals((string) "ArticulateArchive"));
            if (archive == null)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No Articulate Archive node found for the specified id"));
            }

            var list = new List<char> {ActionNew.Instance.Letter, ActionUpdate.Instance.Letter};
            var hasPermission = CheckPermissions(Security.CurrentUser, Services.UserService, list.ToArray(), archive);
            if (hasPermission == false)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Cannot create content at this level"));   
            }

            //parse out the images, we may be posting more than is in the body            
            model.Body = ParseImages(model.Body, multiPartRequest);

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
            
            if (model.Tags.IsNullOrWhiteSpace() == false)
            {
                var tags = model.Tags.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                content.SetTags("tags", tags, true, "ArticulateTags");
            }

            if (model.Categories.IsNullOrWhiteSpace() == false)
            {
                var cats = model.Categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                content.SetTags("categories", cats, true, "ArticulateCategories");
            }

            if (model.Slug.IsNullOrWhiteSpace() == false)
            {
                content.SetValue("umbracoUrlName", model.Slug);
            }
            
            var status = Services.ContentService.SaveAndPublishWithStatus(content, Security.GetUserId());
            if (status.Success == false)
            {
                CleanFiles(multiPartRequest);

                ModelState.AddModelError("server", "Publishing failed: " + status.Result.StatusType);
                //probably  need to send back more info than that...
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }
            
            var published = Umbraco.TypedContent(content.Id);

            CleanFiles(multiPartRequest);

            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(published.UrlWithDomain(), Encoding.UTF8, "text/html");
            return response;
        }

        private void CleanFiles(MultipartFileStreamProvider multiPartRequest)
        {
            foreach (var f in multiPartRequest.FileData)
            {
                File.Delete(f.LocalFileName);
            }
        }

        private string ParseImages(string body, MultipartFileStreamProvider multiPartRequest)
        {
            return Regex.Replace(body, @"\[i:(\d+)\:(.*?)]", m =>
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
        }

        private static bool CheckPermissions(IUser user, IUserService userService, char[] permissionsToCheck, IContent contentItem)
        {
            if (permissionsToCheck == null || !permissionsToCheck.Any()) return true;

            var entityPermission = userService.GetPermissions(user, new[] {contentItem.Id}).FirstOrDefault();

            var flag = true;
            foreach (var ch in permissionsToCheck)
            {
                if (entityPermission == null || !entityPermission.AssignedPermissions.Contains(ch.ToString(CultureInfo.InvariantCulture)))
                    flag = false;
            }
            return flag;
        }
    }
}