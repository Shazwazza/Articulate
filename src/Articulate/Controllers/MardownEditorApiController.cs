using Articulate.Models;
using Newtonsoft.Json;
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
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Actions;
using Umbraco.Web.Composing;
using Umbraco.Web.WebApi;
using File = System.IO.File;

namespace Articulate.Controllers
{
    public class MardownEditorApiController : UmbracoAuthorizedApiController
    {
        private readonly IMediaFileSystem _mediaFileSystem;

        public MardownEditorApiController(IGlobalSettings globalSettings, IUmbracoContextAccessor umbracoContextAccessor, ISqlContext sqlContext, ServiceContext services, AppCaches appCaches, IProfilingLogger logger, IRuntimeState runtimeState, UmbracoHelper umbracoHelper, IMediaFileSystem mediaFileSystem) : base(globalSettings, umbracoContextAccessor, sqlContext, services, appCaches, logger, runtimeState, umbracoHelper)
        {
            _mediaFileSystem = mediaFileSystem;
        }

        public class ParseImageResponse
        {
            public string BodyText { get; set; }
            public string FirstImage { get; set; }
        }

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

            var extractFirstImageAsProperty = true;
            if (articulateNode.HasProperty("extractFirstImage"))
            {
                extractFirstImageAsProperty = articulateNode.GetValue<bool>("extractFirstImage");
            }

            var archive = Services.ContentService.GetPagedChildren(model.ArticulateNodeId.Value, 0, int.MaxValue, out long totalArchiveNodes)                
                .FirstOrDefault(x => x.ContentType.Alias.InvariantEquals("ArticulateArchive"));
            if (archive == null)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No Articulate Archive node found for the specified id"));
            }

            var list = new List<char> { ActionNew.ActionLetter, ActionUpdate.ActionLetter };
            var hasPermission = CheckPermissions(Security.CurrentUser, Services.UserService, list.ToArray(), archive);
            if (hasPermission == false)
            {
                CleanFiles(multiPartRequest);
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Cannot create content at this level"));
            }

            //parse out the images, we may be posting more than is in the body
            var parsedImageResponse = ParseImages(model.Body, multiPartRequest, extractFirstImageAsProperty);

            model.Body = parsedImageResponse.BodyText;

            var content = Services.ContentService.CreateContent(
                model.Title,
                archive.GetUdi(),
                "ArticulateMarkdown",
                UmbracoContext.Security.GetUserId().Result);

            content.SetValue("markdown", model.Body);

            if (!string.IsNullOrEmpty(parsedImageResponse.FirstImage))
            {
                content.SetValue("postImage", parsedImageResponse.FirstImage);
            }

            if (model.Excerpt.IsNullOrWhiteSpace() == false)
            {
                content.SetValue("excerpt", model.Excerpt);
            }

            if (model.Tags.IsNullOrWhiteSpace() == false)
            {
                var tags = model.Tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                content.AssignTags("tags", tags, true, "ArticulateTags");
            }

            if (model.Categories.IsNullOrWhiteSpace() == false)
            {
                var cats = model.Categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                content.AssignTags("categories", cats, true, "ArticulateCategories");
            }

            if (model.Slug.IsNullOrWhiteSpace() == false)
            {
                content.SetValue("umbracoUrlName", model.Slug);
            }

            var status = Services.ContentService.SaveAndPublish(content, userId: UmbracoContext.Security.GetUserId().Result);
            if (status.Success == false)
            {
                CleanFiles(multiPartRequest);

                ModelState.AddModelError("server", "Publishing failed: " + status.Result);
                //probably  need to send back more info than that...
                throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            }

            var published = Umbraco.Content(content.Id);

            CleanFiles(multiPartRequest);

            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(published.Url, Encoding.UTF8, "text/html");
            return response;
        }

        private static void CleanFiles(MultipartFileStreamProvider multiPartRequest)
        {
            foreach (var f in multiPartRequest.FileData)
            {
                File.Delete(f.LocalFileName);
            }
        }

        private ParseImageResponse ParseImages(string body, MultipartFileStreamProvider multiPartRequest, bool extractFirstImageAsProperty)
        {
            var firstImage = string.Empty;
            var bodyText = Regex.Replace(body, @"\[i:(\d+)\:(.*?)]", m =>
            {
                var index = m.Groups[1].Value.TryConvertTo<int>();
                if (index)
                {
                    //get the file at this index
                    var file = multiPartRequest.FileData[index.Result];

                    var rndId = Guid.NewGuid().ToString("N");

                    using (var stream = File.OpenRead(file.LocalFileName))
                    {
                        var fileUrl = "articulate/" + rndId + "/" + file.Headers.ContentDisposition.FileName.TrimStart("\"").TrimEnd("\"");

                        _mediaFileSystem.AddFile(fileUrl, stream);
                        
                        var result = string.Format("![{0}]({1})",
                            fileUrl,
                            fileUrl
                        );

                        if (extractFirstImageAsProperty && string.IsNullOrEmpty(firstImage))
                        {
                            firstImage = fileUrl;
                            //in this case, we've extracted the image, we don't want it to be displayed
                            // in the content too so don't return it.
                            return string.Empty;
                        }

                        return result;
                    }
                }

                return m.Value;
            });

            return new ParseImageResponse { BodyText = bodyText, FirstImage = firstImage };
        }

        private static bool CheckPermissions(IUser user, IUserService userService, char[] permissionsToCheck, IContent contentItem)
        {
            if (permissionsToCheck == null || !permissionsToCheck.Any()) return true;

            var entityPermission = userService.GetPermissions(user, new[] { contentItem.Id }).FirstOrDefault();

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