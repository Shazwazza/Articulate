#nullable enable
using System.Text.Json;
using System.Text.RegularExpressions;
using Articulate.Attributes;
using Articulate.Models.Api;
using Articulate.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Authorization;

namespace Articulate.Controllers.Api
{
    /// <summary>
    ///     Controller for handling the a-new markdown editor endpoint for creating blog posts.
    /// </summary>
    [ManagementApi(ArticulateConstants.ManagementApi.MarkdownEditor)]
    [ApiVersion("1.0")]
    [Authorize(AuthorizationPolicies.BackOfficeAccess)]
    [MapToApi(ArticulateConstants.ManagementApi.Name)]
    [ManagementApiRoute("editors/markdown")]
    public class MarkdownEditorApiController(
        BackOfficeAuthService backOfficeAuthService,
        UmbracoHelper umbracoHelper,
        PropertyEditorCollection propertyEditors,
        IJsonSerializer jsonSerializer,
        ILanguageService languageService,
        IContentService contentService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        ILogger<MarkdownEditorApiController> logger,
        IAbsoluteUrlBuilder absoluteUrlBuilder,
        IArticulateImportMediaService service
#if UMBRACO_18_OR_GREATER
        , IIdKeyMap idKeyMap
#endif
        )
        : ManagementApiControllerBase
    {
        /// <summary>
        ///     Creates a new post under the specified Articulate node.
        /// </summary>
        /// <param name="jsonModel">
        ///     The JSON model containing the post data: Title, Body, Slug, Excerpt, Tags, Categories,
        ///     ArticulateBlogNode, and whether the first image should be extracted as a dedicated property.
        /// </param>
        /// <returns>A <see cref="CreatePostResponse" /> containing the URL of the newly created post.</returns>
        [HttpPost("post")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(CreatePostResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<CreatePostResponse>> CreatePost(
            [FromForm(Name = "json")] string jsonModel)
        {
            if (ValidateAndDeserializeModel(jsonModel, out MarkdownEditorModel? model) is { } validationError)
            {
                return validationError;
            }

            if (GetArticulateNodes(model!, out IContent? articulateNode, out IContent? archive) is { } nodeError)
            {
                return nodeError;
            }

            IUser? currentUser = backOfficeAuthService.GetCurrentUser();
            if (CheckPermissions(archive!, currentUser) is { } permissionError)
            {
                return permissionError;
            }

            bool extractFirstImageAsProperty = articulateNode!.HasProperty("extractFirstImage")
                                               && articulateNode.GetValue<bool>("extractFirstImage");

            ParseImageResponse parsedImageResponse = await ParseImages(
                model!.Body,
                Request.Form.Files,
                extractFirstImageAsProperty);

            model.Body = parsedImageResponse.BodyText;

            return await CreateAndSaveContentAsync(model, archive!, parsedImageResponse, currentUser!);
        }

        private ActionResult? ValidateAndDeserializeModel(string jsonModel, out MarkdownEditorModel? model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(jsonModel))
            {
                return Problem(
                    "The 'json' form part is missing or empty.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                model = JsonSerializer.Deserialize<MarkdownEditorModel>(jsonModel, jsonOptions);
                if (model is null)
                {
                    return Problem("The provided JSON model is invalid.", statusCode: StatusCodes.Status400BadRequest);
                }

                return null;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "JSON deserialization failed for markdown editor create post request.");
                return Problem(
                    "The provided JSON model could not be parsed.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        private ActionResult? GetArticulateNodes(
            MarkdownEditorModel model,
            out IContent? articulateNode,
            out IContent? archive)
        {
            articulateNode = contentService.GetById(model.ArticulateBlogNode);
            archive = null;

            if (articulateNode is null)
            {
                return Problem(
                    $"No Articulate node found with the specified id: {model.ArticulateBlogNode}",
                    statusCode: StatusCodes.Status404NotFound);
            }

            archive = contentService.GetPagedChildrenCompat(model.ArticulateBlogNode, 0, 1, out _)
                .FirstOrDefault(x =>
                    x.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateArchive));

            if (archive is null)
            {
                return Problem(
                    "No Articulate Archive node found for the specified id.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return null;
        }

        private ActionResult? CheckPermissions(IContent archive, IUser? currentUser)
        {
            if (currentUser is null)
            {
                return Unauthorized();
            }

            var requiredPermissions = new[] { ActionNew.ActionLetter, ActionPublish.ActionLetter };
            if (!backOfficeAuthService.HasPermissions(currentUser, archive, requiredPermissions))
            {
                return Forbid();
            }

            return null;
        }

        private async Task<ActionResult<CreatePostResponse>> CreateAndSaveContentAsync(
            MarkdownEditorModel model,
            IContent archive,
            ParseImageResponse parsedImageResponse,
            IUser currentUser)
        {
            IContentType? contentType = contentTypeService.Get("ArticulateMarkdown");
            if (contentType is null)
            {
                logger.LogError("Server configuration error: The 'ArticulateMarkdown' content type was not found.");
                return Problem(
                    "Server configuration error: The 'ArticulateMarkdown' content type was not found.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            IContent content = await contentService.CreateWithInvariantOrDefaultCultureNameAsync(
                model.Title,
                archive,
                contentType,
                languageService,
                logger,
                currentUser.Id);

            await PopulateContentPropertiesAsync(
                content,
                contentType,
                model,
                parsedImageResponse.FirstImage,
                currentUser);

            ActionResult? saveAndPublishResult = SaveAndPublishContent(content, currentUser.Id);
            if (saveAndPublishResult is not null)
            {
                return saveAndPublishResult;
            }

            IPublishedContent? published = umbracoHelper.Content(content.Id);
            return Ok(new CreatePostResponse { Url = published?.Url() ?? string.Empty });
        }

        private async Task<ParseImageResponse> ParseImages(
            string? body,
            IFormFileCollection formFiles,
            bool extractFirstImageAsProperty)
        {
            if (body is null)
            {
                return new ParseImageResponse();
            }

            var firstImage = string.Empty;
            var bodyText = body;
            var replacementMap = new Dictionary<string, string>();
            var firstImageCaptured = false;

            MatchCollection matches = ArticulateMarkdownEditorRegexes.ImageTagPlaceholderRegex().Matches(body);

            foreach (Match match in matches)
            {
                ImageProcessResult result = await ProcessImageMatchAsync(
                    match,
                    formFiles,
                    extractFirstImageAsProperty && !firstImageCaptured);

                if (result.IsFirstImage && !string.IsNullOrEmpty(result.FirstImageUdi))
                {
                    firstImage = result.FirstImageUdi;
                    firstImageCaptured = true;
                }

                replacementMap[match.Value] =
                    result.IsFirstImage && !string.IsNullOrEmpty(result.FirstImageUdi)
                        ? string.Empty
                        : result.ReplacementMarkdown;
            }

            if (replacementMap.Count > 0)
            {
                bodyText = ArticulateMarkdownEditorRegexes.ImageTagPlaceholderRegex().Replace(
                    body,
                    m => replacementMap.TryGetValue(m.Value, out var replacement) ? replacement : m.Value);
            }

            return new ParseImageResponse { BodyText = bodyText, FirstImage = firstImage };
        }

        private async Task<ImageProcessResult> ProcessImageMatchAsync(
            Match match,
            IFormFileCollection formFiles,
            bool saveAsFirstImage)
        {
            var userLabel = match.Groups[1].Value;
            var tempUrl = match.Groups[2].Value;

            IFormFile? file = formFiles.FirstOrDefault(f => f.Name == tempUrl);
            if (file is null)
            {
                logger.LogWarning(
                    "Markdown image placeholder for {TempUrl} found, but no corresponding file was uploaded.", tempUrl);
                return ImageProcessResult.Removed();
            }

            var originalFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

            await using Stream uploadStream = file.OpenReadStream();
            ImportMediaValidationResult validationResult = await service.ValidateImageAsync(
                uploadStream,
                extension);

            if (!validationResult.IsValid)
            {
                logger.LogWarning(
                    "Markdown image {FileName} rejected: {ErrorMessage}",
                    originalFileName,
                    validationResult.ErrorMessage);
                return ImageProcessResult.Removed();
            }

            // Maintain user-provided label for alt text, falling back to original filename
            var altText = string.IsNullOrWhiteSpace(userLabel) ? originalFileName : userLabel.Trim();

            if (saveAsFirstImage)
            {
                return SaveImageToMediaLibrary(
                    validationResult.ValidatedStream!,
                    altText,
                    validationResult.CorrectExtension!);
            }

            var absoluteUrl = service.SaveToFileSystem(
                validationResult.ValidatedStream!,
                validationResult.CorrectExtension!);

            if (string.IsNullOrEmpty(absoluteUrl))
            {
                logger.LogWarning(
                    "Failed to save markdown image {FileName} to filesystem - returned empty URL",
                    Path.GetFileName(file.FileName));
                return ImageProcessResult.Removed();
            }

            return ImageProcessResult.RegularImage($"![{altText}]({absoluteUrl})");
        }

        private ImageProcessResult SaveImageToMediaLibrary(
            Stream stream,
            string altText,
            string extension)
        {
            ImportMediaSaveResult saveResult = service.SaveToMediaLibrary(
                stream,
                altText,
                extension,
                service.GetOrCreateArticulateMediaFolder());

            if (!saveResult.Success || saveResult.Media is null)
            {
                logger.LogWarning(
                    "Failed to save media item for first image: {ErrorMessage}",
                    saveResult.ErrorMessage);
                return ImageProcessResult.Removed();
            }

            IPublishedContent? media = umbracoHelper.Media(saveResult.Media.Key);
            if (media is null)
            {
                logger.LogWarning(
                    "Failed to retrieve published media for first image: {MediaKey}",
                    saveResult.Media.Key);
                return ImageProcessResult.Removed();
            }

            var mediaUrl = media.Url();
            if (string.IsNullOrEmpty(mediaUrl))
            {
                logger.LogWarning("Media URL is empty for first image: {MediaKey}", saveResult.Media.Key);
                return ImageProcessResult.Removed();
            }

            var absoluteMediaUrl = absoluteUrlBuilder.ToAbsoluteUrl(mediaUrl).ToString();

            return ImageProcessResult.FirstImage(saveResult.MediaUdi!, $"![{altText}]({absoluteMediaUrl})");
        }

        private class ImageProcessResult
        {
            public bool IsFirstImage { get; private init; }
            public string? FirstImageUdi { get; private init; }
            public string ReplacementMarkdown { get; private init; } = string.Empty;

            public static ImageProcessResult FirstImage(string udi, string markdown) =>
                new() { IsFirstImage = true, FirstImageUdi = udi, ReplacementMarkdown = markdown };

            public static ImageProcessResult RegularImage(string markdown) =>
                new() { IsFirstImage = false, ReplacementMarkdown = markdown };

            public static ImageProcessResult Removed() =>
                new() { IsFirstImage = false, ReplacementMarkdown = string.Empty };
        }

        private class ParseImageResponse
        {
            public string BodyText { get; init; } = string.Empty;

            public string FirstImage { get; init; } = string.Empty;
        }

        private async Task PopulateContentPropertiesAsync(
            IContent content,
            IContentType contentType,
            MarkdownEditorModel model,
            string? firstImageUdi,
            IUser currentUser)
        {
            await content.SetInvariantOrDefaultCultureValueAsync(
                "markdown",
                model.Body,
                contentType,
                languageService,
                logger);

            if (!string.IsNullOrEmpty(firstImageUdi))
            {
                await content
                    .SetInvariantOrDefaultCultureValueAsync(
                        "postImage",
                        firstImageUdi,
                        contentType,
                        languageService,
                        logger);
            }

            if (!model.Excerpt.IsNullOrWhiteSpace())
            {
                await content
                    .SetInvariantOrDefaultCultureValueAsync(
                        "excerpt",
                        model.Excerpt,
                        contentType,
                        languageService,
                        logger);
            }

            if (!model.Tags.IsNullOrWhiteSpace())
            {
                IEnumerable<string> tags = model.Tags.Split([','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());
                await content.AssignInvariantOrDefaultCultureTagsAsync(
                    "tags",
                    tags,
                    contentType,
                    languageService,
                    dataTypeService,
                    propertyEditors,
                    jsonSerializer,
#if UMBRACO_18_OR_GREATER
                    idKeyMap,
#endif
                    logger);
            }

            if (!model.Categories.IsNullOrWhiteSpace())
            {
                IEnumerable<string> cats = model.Categories.Split([','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());
                await content.AssignInvariantOrDefaultCultureTagsAsync(
                    "categories",
                    cats,
                    contentType,
                    languageService,
                    dataTypeService,
                    propertyEditors,
                    jsonSerializer,
#if UMBRACO_18_OR_GREATER
                    idKeyMap,
#endif
                    logger);
            }

            if (!model.Slug.IsNullOrWhiteSpace())
            {
                await content.SetInvariantOrDefaultCultureValueAsync(
                    Umbraco.Cms.Core.Constants.Conventions.Content.UrlName,
                    model.Slug,
                    contentType,
                    languageService,
                    logger);
            }

            await content.SetInvariantOrDefaultCultureValueAsync(
                "enableComments",
                true,
                contentType,
                languageService,
                logger);

            await content.SetInvariantOrDefaultCultureValueAsync(
                "author",
                currentUser.Name ?? "Unknown",
                contentType,
                languageService,
                logger);
        }

        private ActionResult? SaveAndPublishContent(IContent content, int authorId)
        {
            OperationResult saveStatus = contentService.Save(content, authorId);
            if (!saveStatus.Success)
            {
                ModelState.AddModelError("SaveOperation", "Content failed to save. Please check logs for details.");
                return ValidationProblem(ModelState);
            }

            PublishResult publishStatus = contentService.Publish(content, ["*"], authorId);
            if (!publishStatus.Success)
            {
                ModelState.AddModelError("SaveOperation", "Content failed to publish. Please check logs for details.");
                return ValidationProblem(ModelState);
            }

            return null;
        }
    }
}
