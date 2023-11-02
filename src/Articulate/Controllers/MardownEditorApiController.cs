using Articulate.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    /// <summary>
    /// Controller for handling the a-new markdown editor endpoint for creating blog posts
    /// </summary>
    public class MardownEditorApiController : UmbracoAuthorizedApiController
    {
        private readonly ServiceContext _services;
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly MediaFileManager _mediaFileManager;
        private readonly PropertyEditorCollection _propertyEditors;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly GlobalSettings _globalSettings;
        private readonly IHostingEnvironment _hostingEnvironment;

        public MardownEditorApiController(
            ServiceContext services,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
            UmbracoHelper umbracoHelper,
            MediaFileManager mediaFileManager,
            PropertyEditorCollection propertyEditors,
            IJsonSerializer jsonSerializer,
            IOptions<GlobalSettings> globalSettings,
            IHostingEnvironment hostingEnvironment)
        {
            _services = services;
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _umbracoHelper = umbracoHelper;
            _mediaFileManager = mediaFileManager;
            _propertyEditors = propertyEditors;
            _jsonSerializer = jsonSerializer;
            _globalSettings = globalSettings.Value;
            _hostingEnvironment = hostingEnvironment;
        }

        public class ParseImageResponse
        {
            public string BodyText { get; set; }
            public string FirstImage { get; set; }
        }

        public async Task<ActionResult> PostNew()
        {
            await Task.CompletedTask;

            if (!Request.HasFormContentType && !Request.Form.Files.Any())
            {
                return StatusCode((int)HttpStatusCode.UnsupportedMediaType);
            }

            if (Request.Form.ContainsKey("model") == false)
            {
                return BadRequest("The request was not formatted correctly and is missing the 'model' parameter");
            }

            var model = JsonConvert.DeserializeObject<MardownEditorModel>(Request.Form["model"]);

            if (model.ArticulateNodeId.HasValue == false)
            {
                return BadRequest("No id specified");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var articulateNode = _services.ContentService.GetById(model.ArticulateNodeId.Value);
            if (articulateNode == null)
            {
                return BadRequest("No Articulate node found with the specified id");
            }

            var extractFirstImageAsProperty = true;
            if (articulateNode.HasProperty("extractFirstImage"))
            {
                extractFirstImageAsProperty = articulateNode.GetValue<bool>("extractFirstImage");
            }

            var archive = _services.ContentService.GetPagedChildren(model.ArticulateNodeId.Value, 0, int.MaxValue, out long totalArchiveNodes)
                .FirstOrDefault(x => x.ContentType.Alias.InvariantEquals(ArticulateConstants.ArticulateArchiveContentTypeAlias));
            if (archive == null)
            {
                return BadRequest("No Articulate Archive node found for the specified id");
            }

            var list = new List<char> { ActionNew.ActionLetter, ActionUpdate.ActionLetter };
            var hasPermission = CheckPermissions(
                _backOfficeSecurityAccessor.BackOfficeSecurity.CurrentUser,
                _services.UserService,
                list.ToArray(),
                archive);

            if (hasPermission == false)
            {
                return BadRequest("Cannot create content at this level");
            }

            //parse out the images, we may be posting more than is in the body
            var parsedImageResponse = ParseImages(model.Body, Request.Form.Files, extractFirstImageAsProperty);

            model.Body = parsedImageResponse.BodyText;

            var contentType = _services.ContentTypeService.Get("ArticulateMarkdown");
            if (contentType == null)
            {
                return BadRequest("No ArticulateMarkdown content type found");
            }

            var content = _services.ContentService.CreateWithInvariantOrDefaultCultureName(
                model.Title,
                archive,
                contentType,
                _services.LocalizationService,
                _backOfficeSecurityAccessor.BackOfficeSecurity.GetUserId().Result);

            content.SetInvariantOrDefaultCultureValue("markdown", model.Body, contentType, _services.LocalizationService);

            if (!string.IsNullOrEmpty(parsedImageResponse.FirstImage))
            {
                content.SetInvariantOrDefaultCultureValue("postImage", parsedImageResponse.FirstImage, contentType, _services.LocalizationService);
            }

            if (model.Excerpt.IsNullOrWhiteSpace() == false)
            {
                content.SetInvariantOrDefaultCultureValue("excerpt", model.Excerpt, contentType, _services.LocalizationService);
            }

            if (model.Tags.IsNullOrWhiteSpace() == false)
            {
                var tags = model.Tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                content.AssignInvariantOrDefaultCultureTags("tags", tags, contentType, _services.LocalizationService, _services.DataTypeService, _propertyEditors, _jsonSerializer);
            }

            if (model.Categories.IsNullOrWhiteSpace() == false)
            {
                var cats = model.Categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
                content.AssignInvariantOrDefaultCultureTags("categories", cats, contentType, _services.LocalizationService, _services.DataTypeService, _propertyEditors, _jsonSerializer);
            }

            if (model.Slug.IsNullOrWhiteSpace() == false)
            {
                content.SetInvariantOrDefaultCultureValue("umbracoUrlName", model.Slug, contentType, _services.LocalizationService);
            }

            //author is required
            content.SetInvariantOrDefaultCultureValue("author", _backOfficeSecurityAccessor.BackOfficeSecurity.CurrentUser.Name ?? "Unknown", contentType, _services.LocalizationService);

            var status = _services.ContentService.SaveAndPublish(content, userId: _backOfficeSecurityAccessor.BackOfficeSecurity.GetUserId().Result);
            if (status.Success == false)
            {
                ModelState.AddModelError("server", "Publishing failed: " + status.Result);
                //probably  need to send back more info than that...
                return BadRequest(ModelState);                
            }

            IPublishedContent published = _umbracoHelper.Content(content.Id);
            return Ok(new { url = published.Url() });
        }
        

        private ParseImageResponse ParseImages(string body, IFormFileCollection formFiles, bool extractFirstImageAsProperty)
        {
            var firstImage = string.Empty;
            var bodyText = Regex.Replace(body, @"\[i:(\d+)\:(.*?)]", m =>
            {
                var index = m.Groups[1].Value.TryConvertTo<int>();
                if (index)
                {
                    //get the file at this index
                    var file = formFiles[index.Result];

                    var rndId = Guid.NewGuid().ToString("N");

                    using (var stream = new MemoryStream())
                    {
                        file.CopyTo(stream);

                        var fileUrl = "articulate/" + rndId + "/" + file.FileName.TrimStart("\"").TrimEnd("\"");
                        _mediaFileManager.FileSystem.AddFile(fileUrl, stream);

                        // UmbracoMediaPath default setting = ~/media
                        // Resolved mediaRootPath = /media
                        var mediaRootPath = _hostingEnvironment.ToAbsolute(_globalSettings.UmbracoMediaPath);
                        var mediaFilePath = $"{mediaRootPath}/{fileUrl}";
                        var result = $"![{mediaFilePath}]({mediaFilePath})";

                        if (extractFirstImageAsProperty && string.IsNullOrEmpty(firstImage))
                        {
                            firstImage = mediaFilePath;

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

            if (permissionsToCheck == null || !permissionsToCheck.Any())
            {
                return true;
            }

            var entityPermission = userService.GetPermissions(user, new[] { contentItem.Id }).FirstOrDefault();

            var flag = true;
            foreach (var ch in permissionsToCheck)
            {
                if (entityPermission == null || !entityPermission.AssignedPermissions.Contains(ch.ToString(CultureInfo.InvariantCulture)))
                {
                    flag = false;
                }
            }

            return flag;
        }
    }
}
