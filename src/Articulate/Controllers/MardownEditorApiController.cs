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
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    /// <summary>
    /// Controller for handling the a-new mardown editor endpoint for creating blog posts
    /// </summary>
    public class MardownEditorApiController : UmbracoAuthorizedApiController
    {
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly ServiceContext _services;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly MediaFileManager _mediaFileManager;

        public MardownEditorApiController(
            IHostingEnvironment hostingEnvironment,
            ServiceContext services,
            IUmbracoContextAccessor umbracoContextAccessor,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
            UmbracoHelper umbracoHelper,
            MediaFileManager mediaFileManager)
        {
            _hostingEnvironment = hostingEnvironment;
            _services = services;
            _umbracoContextAccessor = umbracoContextAccessor;
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _umbracoHelper = umbracoHelper;
            _mediaFileManager = mediaFileManager;
        }

        public class ParseImageResponse
        {
            public string BodyText { get; set; }
            public string FirstImage { get; set; }
        }

        public async Task<HttpResponseMessage> PostNew()
        {
            throw new NotImplementedException("TODO: Implement markdown editor");

            //if (!Request.Content.IsMimeMultipartContent())
            //    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);

            //var str = _hostingEnvironment.MapPathContentRoot("~/Umbraco/Data/TEMP/FileUploads");
            //Directory.CreateDirectory(str);
            //var provider = new MultipartFormDataStreamProvider(str);

            //var multiPartRequest = await Request.Content.ReadAsMultipartAsync(provider);

            //if (multiPartRequest.FormData["model"] == null)
            //{
            //    CleanFiles(multiPartRequest);
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "The request was not formatted correctly and is missing the 'model' parameter"));
            //}

            //var model = JsonConvert.DeserializeObject<MardownEditorModel>(multiPartRequest.FormData["model"]);

            //if (model.ArticulateNodeId.HasValue == false)
            //{
            //    CleanFiles(multiPartRequest);
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No id specified"));
            //}

            //if (!ModelState.IsValid)
            //{
            //    CleanFiles(multiPartRequest);
            //    throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            //}

            //var articulateNode = _services.ContentService.GetById(model.ArticulateNodeId.Value);
            //if (articulateNode == null)
            //{
            //    CleanFiles(multiPartRequest);
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No Articulate node found with the specified id"));
            //}

            //var extractFirstImageAsProperty = true;
            //if (articulateNode.HasProperty("extractFirstImage"))
            //{
            //    extractFirstImageAsProperty = articulateNode.GetValue<bool>("extractFirstImage");
            //}

            //var archive = _services.ContentService.GetPagedChildren(model.ArticulateNodeId.Value, 0, int.MaxValue, out long totalArchiveNodes)                
            //    .FirstOrDefault(x => x.ContentType.Alias.InvariantEquals(ArticulateConstants.ArticulateArchiveContentTypeAlias));
            //if (archive == null)
            //{
            //    CleanFiles(multiPartRequest);
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No Articulate Archive node found for the specified id"));
            //}

            //var list = new List<char> { ActionNew.ActionLetter, ActionUpdate.ActionLetter };
            //var hasPermission = CheckPermissions(
            //    _backOfficeSecurityAccessor.BackOfficeSecurity.CurrentUser,
            //    _services.UserService,
            //    list.ToArray(),
            //    archive);

            //if (hasPermission == false)
            //{
            //    CleanFiles(multiPartRequest);
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Cannot create content at this level"));
            //}

            ////parse out the images, we may be posting more than is in the body
            //var parsedImageResponse = ParseImages(model.Body, multiPartRequest, extractFirstImageAsProperty);

            //model.Body = parsedImageResponse.BodyText;

            //var contentType = _services.ContentTypeService.Get("ArticulateMarkdown");
            //if (contentType == null)
            //{
            //    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Forbidden, "No ArticulateMarkdown content type found"));
            //}
            //var content = _services.ContentService.CreateWithInvariantOrDefaultCultureName(
            //    model.Title,
            //    archive,
            //    contentType,
            //    _services.LocalizationService,
            //    _backOfficeSecurityAccessor.BackOfficeSecurity.GetUserId().Result);

            //content.SetInvariantOrDefaultCultureValue("markdown", model.Body, contentType, _services.LocalizationService);

            //if (!string.IsNullOrEmpty(parsedImageResponse.FirstImage))
            //{
            //    content.SetInvariantOrDefaultCultureValue("postImage", parsedImageResponse.FirstImage, contentType, _services.LocalizationService);
            //}

            //if (model.Excerpt.IsNullOrWhiteSpace() == false)
            //{
            //    content.SetInvariantOrDefaultCultureValue("excerpt", model.Excerpt, contentType, _services.LocalizationService);
            //}

            //if (model.Tags.IsNullOrWhiteSpace() == false)
            //{
            //    var tags = model.Tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
            //    content.AssignInvariantOrDefaultCultureTags("tags", tags, contentType, _services.LocalizationService);
            //}

            //if (model.Categories.IsNullOrWhiteSpace() == false)
            //{
            //    var cats = model.Categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
            //    content.AssignInvariantOrDefaultCultureTags("categories", cats, contentType, _services.LocalizationService);
            //}

            //if (model.Slug.IsNullOrWhiteSpace() == false)
            //{
            //    content.SetInvariantOrDefaultCultureValue("umbracoUrlName", model.Slug, contentType, _services.LocalizationService);
            //}

            ////author is required
            //content.SetInvariantOrDefaultCultureValue("author", _backOfficeSecurityAccessor.BackOfficeSecurity.CurrentUser.Name ?? "Unknown", contentType, _services.LocalizationService);

            //var status = _services.ContentService.SaveAndPublish(content, userId: _backOfficeSecurityAccessor.BackOfficeSecurity.GetUserId().Result);
            //if (status.Success == false)
            //{
            //    CleanFiles(multiPartRequest);

            //    ModelState.AddModelError("server", "Publishing failed: " + status.Result);
            //    //probably  need to send back more info than that...
            //    throw new HttpResponseException(Request.CreateValidationErrorResponse(ModelState));
            //}

            //IPublishedContent published = _umbracoHelper.Content(content.Id);

            //CleanFiles(multiPartRequest);

            //var response = Request.CreateResponse(HttpStatusCode.OK);
            //response.Content = new StringContent(published.Url(), Encoding.UTF8, "text/html");
            //return response;
        }

        //private static void CleanFiles(MultipartFileStreamProvider multiPartRequest)
        //{
        //    foreach (var f in multiPartRequest.FileData)
        //    {
        //        System.IO.File.Delete(f.LocalFileName);
        //    }
        //}

        //private ParseImageResponse ParseImages(string body, MultipartFileStreamProvider multiPartRequest, bool extractFirstImageAsProperty)
        //{
        //    var firstImage = string.Empty;
        //    var bodyText = Regex.Replace(body, @"\[i:(\d+)\:(.*?)]", m =>
        //    {
        //        var index = m.Groups[1].Value.TryConvertTo<int>();
        //        if (index)
        //        {
        //            //get the file at this index
        //            var file = multiPartRequest.FileData[index.Result];

        //            var rndId = Guid.NewGuid().ToString("N");

        //            using (var stream = System.IO.File.OpenRead(file.LocalFileName))
        //            {
        //                var fileUrl = "articulate/" + rndId + "/" + file.Headers.ContentDisposition.FileName.TrimStart("\"").TrimEnd("\"");

        //                _mediaFileManager.FileSystem.AddFile(fileUrl, stream);
                        
        //                var result = string.Format("![{0}]({1})",
        //                    fileUrl,
        //                    fileUrl
        //                );

        //                if (extractFirstImageAsProperty && string.IsNullOrEmpty(firstImage))
        //                {
        //                    firstImage = fileUrl;
        //                    //in this case, we've extracted the image, we don't want it to be displayed
        //                    // in the content too so don't return it.
        //                    return string.Empty;
        //                }

        //                return result;
        //            }
        //        }

        //        return m.Value;
        //    });

        //    return new ParseImageResponse { BodyText = bodyText, FirstImage = firstImage };
        //}

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
