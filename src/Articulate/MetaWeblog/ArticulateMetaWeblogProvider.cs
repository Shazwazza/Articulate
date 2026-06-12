#nullable enable
using System.Globalization;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using Articulate.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using WilderMinds.MetaWeblog;
using Tag = WilderMinds.MetaWeblog.Tag;

namespace Articulate.MetaWeblog
{
    /// <summary>
    /// MetaWeblog API provider for Articulate.
    /// </summary>
    public class ArticulateMetaWeblogProvider(
        IUmbracoContextAccessor umbracoContextAccessor,
        IUserService userService,
        IContentTypeService contentTypeService,
        ILanguageService languageService,
        IBackOfficeUserManager backOfficeUserManager,
        IContentService contentService,
        IShortStringHelper shortStringHelper,
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditors,
        IJsonSerializer jsonSerializer,
        MediaFileManager mediaFileManager,
        IPublishedValueFallback publishedValueFallback,
        ILogger<ArticulateMetaWeblogProvider> logger,
        int articulateBlogRootNodeId,
        IArticulateImportMediaService service,
        IArticulateMarkdownConverter articulateMarkdownConverter,
        ArticulateTagService articulateTagService,
        BackOfficeAuthService backOfficeAuthService,
        IHtmlSanitizer htmlSanitizer
#if UMBRACO_18_OR_GREATER
        , IIdKeyMap idKeyMap
#endif
        )
        : IMetaWeblogProvider
    {
        private static readonly char[] _commaSeparator = [','];

        /// <inheritdoc/>
        public Task<int> AddCategoryAsync(string key, string username, string password, NewCategory category) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public Task<string> AddPageAsync(string blogid, string username, string password, Page page, bool publish) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public async Task<string> AddPostAsync(string blogid, string username, string password, Post post, bool publish)
        {
            IUser user = await ValidateUserAsync(username, password);

            IPublishedContent root = BlogRoot();

            IEnumerable<IPublishedContent> archiveNodes =
                root.Children().Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateArchive);
            IPublishedContent node =
                archiveNodes.FirstOrDefault() ??
                throw new InvalidOperationException("No Articulate Archive node found");

            IContentType contentType = contentTypeService.Get(ArticulateConstants.ContentType.ArticulateRichText) ??
                                       throw new InvalidOperationException(
                                           "No content type found with alias 'ArticulateRichText'");

            IContent content = await contentService.CreateWithInvariantOrDefaultCultureNameAsync(
                post.title, node.Id, contentType, languageService, logger, user.Id);

            var extractFirstImageAsProperty = false;
            if (root.HasProperty("extractFirstImage"))
            {
                extractFirstImageAsProperty = root.Value<bool>("extractFirstImage");
            }

            await AddOrUpdateContentAsync(content, contentType, post, user, publish, extractFirstImageAsProperty);

            return content.Id.ToString(CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public Task<bool> DeletePageAsync(string blogid, string username, string password, string pageid) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public async Task<bool> DeletePostAsync(
            string key,
            string postid,
            string username,
            string password,
            bool publish)
        {
            IUser user = await ValidateUserAsync(username, password);
            var userId = user.Id;

            Attempt<int> asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                return false;
            }

            // first see if it's published
            IContent? content = contentService.GetById(asInt.Result);
            if (content is null)
            {
                return false;
            }

            if (!backOfficeAuthService.HasPermissions(user, content, [ActionDelete.ActionLetter]))
            {
                throw new AuthenticationException("User does not have permission to delete this content");
            }

            // Move to recycle bin rather than unpublish
            OperationResult recycleResult = contentService.MoveToRecycleBin(content, userId);
            if (recycleResult.Success)
            {
                return true;
            }

            logger.LogWarning("Failed to move content {ContentId} to recycle bin", content.Id);
            return false;
        }

        /// <inheritdoc/>
        public Task<bool> EditPageAsync(
            string blogid,
            string pageid,
            string username,
            string password,
            Page page,
            bool publish) => throw new NotSupportedException();

        /// <inheritdoc/>
        public async Task<bool> EditPostAsync(string postid, string username, string password, Post post, bool publish)
        {
            IUser user = await ValidateUserAsync(username, password);

            Attempt<int> asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                throw new InvalidOperationException("The id could not be parsed to an integer");
            }

            IContent umbracoContent = contentService.GetById(asInt.Result) ??
                                      throw new InvalidOperationException(
                                          $"The content with id {asInt.Result} could not be found");

            var requiredPermissions = publish
                ? new[] { ActionUpdate.ActionLetter, ActionPublish.ActionLetter }
                : new[] { ActionUpdate.ActionLetter };

            if (!backOfficeAuthService.HasPermissions(user, umbracoContent, requiredPermissions))
            {
                throw new AuthenticationException("User does not have permission to edit this content");
            }

            IContentType contentType = contentTypeService.Get(umbracoContent.ContentType.Alias) ??
                                       throw new InvalidOperationException(
                                           $"No content type found with alias '{umbracoContent.ContentType.Alias}'");

            IPublishedContent root = BlogRoot();

            var extractFirstImageAsProperty = false;
            if (root.HasProperty("extractFirstImage"))
            {
                extractFirstImageAsProperty = root.Value<bool>("extractFirstImage");
            }

            await AddOrUpdateContentAsync(
                umbracoContent,
                contentType,
                post,
                user,
                publish,
                extractFirstImageAsProperty);

            return true;
        }

        /// <inheritdoc/>
        public Task<Author[]> GetAuthorsAsync(string blogid, string username, string password) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public async Task<CategoryInfo[]> GetCategoriesAsync(string blogid, string username, string password)
        {
            _ = await ValidateUserAsync(username, password);

            IEnumerable<ArticulateTagInfo> categories = articulateTagService.GetAllTagInfos(
                BlogRoot().Path,
                ArticulateConstants.DataType.ArticulateCategories);

            return categories.Select(x => new CategoryInfo
            {
                title = x.Name,
                description = x.Name,
                categoryid = x.Id.ToString(CultureInfo.InvariantCulture)
            }).ToArray();
        }

        /// <inheritdoc/>
        public Task<Page> GetPageAsync(string blogid, string pageid, string username, string password) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public Task<Page[]> GetPagesAsync(string blogid, string username, string password, int numPages) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public async Task<Post> GetPostAsync(string postid, string username, string password)
        {
            _ = await ValidateUserAsync(username, password);

            Attempt<int> asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                throw new InvalidOperationException("The id could not be parsed to an integer");
            }

            IPublishedContent? post = umbracoContextAccessor.GetRequiredUmbracoContext().Content.GetById(asInt.Result);
            if (post is not null)
            {
                Post fromPost = FromPost(new PostModel(post, publishedValueFallback));
                return fromPost;
            }

            IContent content = contentService.GetById(asInt.Result) ??
                               throw new InvalidOperationException("No post found with id " + postid);

            Post fromContent = FromContent(content);
            return fromContent;
        }

        /// <inheritdoc/>
        public async Task<Post[]> GetRecentPostsAsync(
            string blogid,
            string username,
            string password,
            int numberOfPosts)
        {
            if (numberOfPosts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numberOfPosts), @"Number of posts must be non-negative");
            }

            numberOfPosts = Math.Min(numberOfPosts, 1000);

            _ = await ValidateUserAsync(username, password);

            IEnumerable<IPublishedContent> archiveNodes =
                BlogRoot().Children().Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateArchive);
            IPublishedContent node =
                archiveNodes.FirstOrDefault() ??
                throw new InvalidOperationException("No Articulate Archive node found");

            Post[] recent =
            [
                .. contentService
                    .GetPagedChildrenCompat(
                        node.Id,
                        0,
                        numberOfPosts,
                        out var _,
                        ordering: Ordering.By("updateDate", Direction.Descending))
                    .Select(FromContent)
            ];

            return recent;
        }

        /// <inheritdoc/>
        public async Task<Tag[]> GetTagsAsync(string blogid, string username, string password)
        {
            _ = await ValidateUserAsync(username, password);

            IEnumerable<string> all = articulateTagService.GetAllTags(
                BlogRoot().Path,
                ArticulateConstants.DataType.ArticulateTags);

            Tag[] tags = [.. all.Select(x => new Tag { name = x })];

            return tags;
        }

        /// <inheritdoc/>
        public Task<UserInfo> GetUserInfoAsync(string key, string username, string password) =>
            throw new NotSupportedException();

        /// <inheritdoc/>
        public async Task<BlogInfo[]> GetUsersBlogsAsync(string key, string username, string password)
        {
            _ = await ValidateUserAsync(username, password);

            IPublishedContent node = BlogRoot();
            BlogInfo[] blogs =
            [
                new() { blogid = node.Id.ToString(), blogName = node.Name, url = node.Url() }
            ];

            return blogs;
        }

        /// <inheritdoc/>
        public async Task<MediaObjectInfo> NewMediaObjectAsync(
            string blogid,
            string username,
            string password,
            MediaObject mediaObject)
        {
            _ = await ValidateUserAsync(username, password);

            if (string.IsNullOrWhiteSpace(mediaObject.bits))
            {
                throw new ArgumentException(@"Invalid file", nameof(mediaObject));
            }

            var fileName = Path.GetFileName(mediaObject.name);
            ImportMediaValidationResult validationResult = await service.DecodeAndValidateBase64ImageAsync(
                mediaObject.bits,
                fileName);

            if (!validationResult.IsValid)
            {
                throw new ArgumentException(
                    $@"Image validation failed: {validationResult.ErrorMessage}",
                    nameof(mediaObject));
            }

            try
            {
                var absoluteUrl = service.SaveToFileSystem(
                    validationResult.ValidatedStream!,
                    validationResult.CorrectExtension!,
                    fileName);

                if (!string.IsNullOrEmpty(absoluteUrl))
                {
                    return new MediaObjectInfo { url = absoluteUrl };
                }

                logger.LogWarning(
                    "Failed to save MetaWeblog image {FileName} to filesystem - SaveToFileSystem returned empty URL",
                    fileName);
                throw new InvalidOperationException(
                    "Failed to save image to filesystem - SaveToFileSystem returned empty URL");
            }
            finally
            {
                if (validationResult.ValidatedStream is not null)
                {
                    await validationResult.ValidatedStream.DisposeAsync();
                }
            }
        }

        private async Task AddOrUpdateContentAsync(
            IContent content,
            IContentType contentType,
            Post post,
            IUser user,
            bool publish,
            bool extractFirstImageAsProperty)
        {
            await content.SetInvariantOrDefaultCultureNameAsync(post.title, contentType, languageService, logger);
            await content.SetInvariantOrDefaultCultureValueAsync(
                "author",
                user.Name,
                contentType,
                languageService,
                logger);

            if (content.HasProperty("richText"))
            {
                await ProcessRichTextContentAsync(content, contentType, post, extractFirstImageAsProperty);
            }

            if (!post.link.IsNullOrWhiteSpace())
            {
                await content.SetInvariantOrDefaultCultureValueAsync(
                    Constants.Conventions.Content.UrlName,
                    post.link,
                    contentType,
                    languageService,
                    logger);
            }

            if (!post.mt_excerpt.IsNullOrWhiteSpace())
            {
                await content
                    .SetInvariantOrDefaultCultureValueAsync(
                        "excerpt",
                        post.mt_excerpt,
                        contentType,
                        languageService,
                        logger);
            }

            await SetCommentSettingsAsync(content, contentType, post.mt_allow_comments);

            await content.AssignInvariantOrDefaultCultureTagsAsync(
                "categories",
                CleanTagValues(post.categories ?? []),
                contentType,
                languageService,
                dataTypeService,
                propertyEditors,
                jsonSerializer,
#if UMBRACO_18_OR_GREATER
                idKeyMap,
#endif
                logger);

            var tags = SplitTagValue(post.mt_keywords);

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

            await SaveAndPublishIfNeededAsync(content, user, post, publish).ConfigureAwait(false);
        }

        /// <summary>
        /// Processes rich text content from MetaWebLog clients.
        /// </summary>
        private async Task ProcessRichTextContentAsync(
            IContent content,
            IContentType contentType,
            Post post,
            bool extractFirstImageAsProperty)
        {
            var cleanedContent = StripInvalidImageUrls(post.description);

            if (cleanedContent != post.description)
            {
                logger.LogWarning(
                    "Stripped invalid protocol URLs from post '{PostName}' - MetaWebLog client may be using local file paths or dangerous protocols",
                    content.Name ?? "New Post");
            }

            Match firstImageMatch = ArticulateMetaWeblogRegexes.MediaSourceRegex().Match(cleanedContent);
            var firstImageRelativePath = firstImageMatch is { Success: true, Groups.Count: 2 }
                ? firstImageMatch.Groups[1].Value
                : string.Empty;

            var contentToSave = UpdateMediaSourceUrls(cleanedContent);
            contentToSave = UpdateMediaHrefUrls(contentToSave);
            contentToSave = htmlSanitizer.Sanitize(contentToSave);

            await content
                .SetInvariantOrDefaultCultureValueAsync(
                    "richText",
                    contentToSave,
                    contentType,
                    languageService,
                    logger);

            if (extractFirstImageAsProperty && content.HasProperty("postImage") &&
                !firstImageRelativePath.IsNullOrWhiteSpace())
            {
                await ExtractAndSaveFirstImageAsync(content, contentType, firstImageRelativePath);
            }
        }

        /// <summary>
        /// Strips images with invalid protocols from HTML.
        /// </summary>
        /// <remarks>
        /// Prevents file:///, javascript:, data:, and protocol-relative URLs.
        /// Only allows http://, https://, and /media/ URLs.
        /// </remarks>
        internal static string StripInvalidImageUrls(string content)
        {
            content = ArticulateMetaWeblogRegexes.InvalidImageParagraphRegex().Replace(content, string.Empty);
            content = ArticulateMetaWeblogRegexes.InvalidImageTagRegex().Replace(content, string.Empty);
            return content;
        }

        private string UpdateMediaSourceUrls(string description) =>
            ArticulateMetaWeblogRegexes.MediaSourceRegex().Replace(description, match =>
            {
                if (match.Groups.Count != 2)
                {
                    return match.Value;
                }

                var relativePath = match.Groups[1].Value;
                var mediaFileSystemPath = mediaFileManager.FileSystem.GetUrl(relativePath);

                return " src=\"" + mediaFileSystemPath + "\"";
            });

        private string UpdateMediaHrefUrls(string content)
        {
            var imagesProcessed = 0;
            return ArticulateMetaWeblogRegexes.MediaHrefRegex().Replace(content, match =>
            {
                if (match.Groups.Count != 2)
                {
                    return match.Value;
                }

                var relativePath = match.Groups[1].Value;
                var mediaFileSystemPath = mediaFileManager.FileSystem.GetUrl(relativePath);

                var href = " href=\"" +
                           mediaFileSystemPath +
                           "\" class=\"a-image-" + imagesProcessed + "\" ";

                imagesProcessed++;

                return href;
            });
        }

        private async Task ExtractAndSaveFirstImageAsync(
            IContent content,
            IContentType contentType,
            string firstImageRelativePath)
        {
            if (!mediaFileManager.FileSystem.FileExists(firstImageRelativePath))
            {
                return;
            }

            try
            {
                await using Stream fileStream = mediaFileManager.FileSystem.OpenFile(firstImageRelativePath);
                var fileName = Path.GetFileName(firstImageRelativePath);
                var extension = Path.GetExtension(fileName);

                ImportMediaSaveResult saveResult = service.SaveToMediaLibrary(
                    fileStream,
                    fileName,
                    extension,
                    service.GetOrCreateArticulateMediaFolder());

                if (saveResult.Success && !string.IsNullOrEmpty(saveResult.MediaUdi))
                {
                    await content.SetInvariantOrDefaultCultureValueAsync(
                        "postImage",
                        saveResult.MediaUdi,
                        contentType,
                        languageService,
                        logger);
                }
                else
                {
                    logger.LogWarning(
                        "Failed to save featured image {FileName}: {ErrorMessage}",
                        fileName,
                        saveResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Could not create media item for featured image {FileName}",
                    firstImageRelativePath);
            }
        }

        private async Task SetCommentSettingsAsync(IContent content, IContentType contentType, int allowComments)
        {
            switch (allowComments)
            {
                case 1:
                    await content
                        .SetInvariantOrDefaultCultureValueAsync(
                            "enableComments",
                            1,
                            contentType,
                            languageService,
                            logger);
                    break;
                case 2:
                    await content
                        .SetInvariantOrDefaultCultureValueAsync(
                            "enableComments",
                            0,
                            contentType,
                            languageService,
                            logger);
                    break;
            }
        }

        private async Task SaveAndPublishIfNeededAsync(IContent content, IUser user, Post post, bool publish)
        {
            if (publish)
            {
                if (post.dateCreated != DateTime.MinValue)
                {
                    IContentType? contentType = contentTypeService.Get(content.ContentTypeId);
                    if (contentType is not null)
                    {
                        await content.SetInvariantOrDefaultCultureValueAsync(
                            "publishedDate",
                            post.dateCreated,
                            contentType,
                            languageService,
                            logger);
                    }
                }

                OperationResult saveAndPublishSaveResult = contentService.Save(content, user.Id);
                saveAndPublishSaveResult.EnsureSuccess(logger, $"save content {content.Id}");

                PublishResult publishResult = contentService.Publish(content, ["*"], user.Id);
                publishResult.EnsureSuccess(logger, $"publish content {content.Id}");
            }
            else
            {
                OperationResult saveResult = contentService.Save(content, user.Id);
                saveResult.EnsureSuccess(logger, $"save content {content.Id}");
            }
        }

        private IPublishedContent BlogRoot()
        {
            IPublishedContent node =
                umbracoContextAccessor.GetRequiredUmbracoContext().Content.GetById(articulateBlogRootNodeId) ??
                throw new InvalidOperationException("No node found by route");

            return node;
        }

        private Post FromContent(IContent post)
        {
            string[] tags = GetTagValues(post, "tags");
            string[] categories = GetTagValues(post, "categories");
            DateTime? publishedDate = post.GetValue<DateTime?>("publishedDate");

            return new Post
            {
                title = post.Name,
                postid = post.Id.ToString(CultureInfo.InvariantCulture),
                dateCreated = publishedDate is { } value && value != default
                    ? value
                    : post.CreateDate != default ? post.CreateDate : post.UpdateDate,
                mt_excerpt = post.GetValue<string>("excerpt"),
                link = string.Empty,
                mt_keywords = string.Join(',', tags),
                categories = categories,
                description = post.ContentType.Alias == ArticulateConstants.ContentType.ArticulateRichText
                    ? post.GetValue<string>("richText")
                    : articulateMarkdownConverter.ToHtml(post.GetValue<string>("markdown") ?? string.Empty),
                permalink = post.GetValue<string>(Constants.Conventions.Content.UrlName).IsNullOrWhiteSpace()
                    ? post.Name?.ToUrlSegment(shortStringHelper)
                    : post.GetValue<string>(Constants.Conventions.Content.UrlName)?.ToUrlSegment(shortStringHelper),
            };
        }

        private static string[] GetTagValues(IContent post, string propertyAlias)
        {
            object? value = post.GetValue(propertyAlias);
            return value switch
            {
                null => [],
                string raw => SplitTagValue(raw),
                IEnumerable<string> values => CleanTagValues(values),
                IEnumerable<object> values => CleanTagValues(values.Select(x => x?.ToString())),
                _ => SplitTagValue(value.ToString()),
            };
        }

        private static string[] SplitTagValue(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? []
                : CleanTagValues(value.Split(_commaSeparator, StringSplitOptions.RemoveEmptyEntries));

        private static string[] CleanTagValues(IEnumerable<string?> values) =>
            values
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        /// <summary>
        ///     There are so many variants of Metaweblog API, so I've just included as many properties, custom ones, etc... that I
        ///     can find.
        /// </summary>
        /// <param name="post">The Articulate post model to convert.</param>
        /// <returns>A MetaWeblog <see cref="Post"/> populated from the Articulate post.</returns>
        /// <remarks>
        ///     http://msdn.microsoft.com/en-us/library/bb463260.aspx
        ///     http://xmlrpc.scripting.com/metaWeblogApi.html
        ///     http://cyber.law.harvard.edu/rss/rss.html#hrelementsOfLtitemgt
        ///     http://codex.wordpress.org/XML-RPC_MetaWeblog_API
        ///     https://blogengine.codeplex.com/SourceControl/latest#BlogEngine/BlogEngine.Core/API/MetaWeblog/MetaWeblogHandler.cs .
        /// </remarks>
        private static Post FromPost(PostModel post) => new()
        {
            categories = [.. post.Categories],
            description = post.Body.ToString() ?? string.Empty,
            dateCreated = post.PublishedDate != default ? post.PublishedDate : post.UpdateDate,
            postid = post.Id.ToString(CultureInfo.InvariantCulture),
            wp_slug = post.Url(),
            mt_excerpt = post.Excerpt,
            mt_keywords = string.Join(',', post.Tags.ToArray()),
            title = post.Name,
        };

        private async Task<IUser> ValidateUserAsync(string username, string password)
        {
            if (!await backOfficeUserManager.ValidateCredentialsAsync(username, password))
            {
                throw new AuthenticationException($"Failed to validate user credentials for {username}");
            }

            IUser user = userService.GetByUsername(username) ??
                         throw new InvalidOperationException($"Failed to find user for {username}");

            return user;
        }
    }
}
