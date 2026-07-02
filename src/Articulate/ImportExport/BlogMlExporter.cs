#nullable enable
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Argotic.Common;
using Argotic.Syndication.Specialized;
using Articulate.Services;
using Articulate.Syndication.BlogML;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;

namespace Articulate.ImportExport
{
    /// <summary>
    /// Exporter for blog content to BlogML format.
    /// </summary>
    public class BlogMlExporter(
        IContentService contentService,
        IMediaService mediaService,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        ITagService tagService,
        MediaFileManager mediaFileManager,
        ArticulateTempFileSystem articulateTempFileSystem,
        IPublishedUrlProvider urlProvider,
        ISqlContext sqlContext,
        ILogger<BlogMlExporter> logger,
        IArticulateMarkdownConverter articulateMarkdownConverter,
        IArticulateRichTextRenderer richTextRenderer)
    {
        /// <summary>
        /// Exports the blog content from a root node to a BlogML file.
        /// </summary>
        /// <param name="blogRootNode">The unique identifier of the Articulate root node.</param>
        /// <param name="exportFileName">The name of the file to create.</param>
        /// <param name="exportImagesAsBase64">If true, images are embedded as Base64 strings.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ExportAsync(
            Guid blogRootNode,
            string exportFileName,
            bool exportImagesAsBase64 = false)
        {
            {
                IContent root = contentService.GetById(blogRootNode) ??
                                throw new InvalidOperationException("No node found with id " + blogRootNode);

                if (!root.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.Articulate))
                {
                    throw new InvalidOperationException(
                        $"The node with id {blogRootNode} is not an Articulate root node");
                }

                _ = contentTypeService.Get(ArticulateConstants.ContentType.ArticulateRichText) ??
                    throw new InvalidOperationException(
                        "Articulate is not installed properly, the 'ArticulateRichText' doc type could not be found");
                IDataType categoryDataType = await dataTypeService.GetAsync("Articulate Categories") ??
                                             throw new InvalidOperationException(
                                                 "No Data Type named 'Articulate Categories' found");
                TagConfiguration? categoryConfiguration = categoryDataType.ConfigurationAs<TagConfiguration>();
                var categoryGroup = categoryConfiguration?.Group;
                IDataType tagDataType = await dataTypeService.GetAsync("Articulate Tags") ??
                                        throw new InvalidOperationException(
                                            "No Data Type named 'Articulate Tags' found");
                TagConfiguration? tagConfiguration = tagDataType.ConfigurationAs<TagConfiguration>();
                var tagGroup = tagConfiguration?.Group;

                var blogMlDoc = new BlogMLDocument
                {
                    RootUrl = new Uri(urlProvider.GetUrl(root.Id), UriKind.RelativeOrAbsolute),
                    GeneratedOn = DateTime.Now,
                    Title = new BlogMLTextConstruct(root.GetValue<string>("blogTitle")),
                    Subtitle = new BlogMLTextConstruct(root.GetValue<string>("blogDescription"))
                };

                IContentType authorsContentType =
                    contentTypeService.Get(ArticulateConstants.ContentType.ArticulateAuthors)
                    ?? throw new InvalidOperationException(
                        "Articulate is not installed properly, the 'ArticulateAuthors' doc type could not be found");

                foreach (IContent authorsNode in EnumerateDescendants(
                             root.Id,
                             sqlContext.Query<IContent>().Where(x => x.ContentTypeId == authorsContentType.Id),
                             Ordering.By("CreateDate", Direction.Descending)))
                {
                    AddBlogAuthors(authorsNode, blogMlDoc);
                }

                AddBlogCategories(blogMlDoc, categoryGroup);

                IContentType archiveContentType =
                    contentTypeService.Get(ArticulateConstants.ContentType.ArticulateArchive)
                    ?? throw new InvalidOperationException(
                        "Articulate is not installed properly, the 'ArticulateArchive' doc type could not be found");

                foreach (IContent archiveNode in EnumerateDescendants(
                             root.Id,
                             sqlContext.Query<IContent>().Where(x => x.ContentTypeId == archiveContentType.Id),
                             Ordering.By("CreateDate", Direction.Descending)))
                {
                    AddBlogPosts(archiveNode, blogMlDoc, categoryGroup, tagGroup, exportImagesAsBase64);
                }

                WriteFile(blogMlDoc, exportFileName);
            }
        }


        private void WriteFile(BlogMLDocument blogMlDoc, string fileName)
        {
            using var stream = new MemoryStream();
            blogMlDoc.Save(stream, new SyndicationResourceSaveSettings { CharacterEncoding = Encoding.UTF8 });
            stream.Position = 0;
            articulateTempFileSystem.AddFile(fileName, stream, true);
        }

        private IEnumerable<IContent> EnumerateDescendants(
            int rootId,
            IQuery<IContent>? filter,
            Ordering? ordering,
            int pageSize = 500)
        {
            var pageIndex = 0;

            while (true)
            {
                IContent[] page = contentService
                    .GetPagedDescendants(rootId, pageIndex++, pageSize, out _, filter, ordering)
                    .ToArray();

                if (page.Length == 0)
                {
                    yield break;
                }

                foreach (IContent item in page)
                {
                    yield return item;
                }

                if (page.Length < pageSize)
                {
                    yield break;
                }
            }
        }

        private void AddBlogCategories(BlogMLDocument blogMlDoc, string? tagGroup)
        {
            IEnumerable<ITag> categories = tagService.GetAllContentTags(tagGroup);
            foreach (ITag category in categories)
            {
                if (category.NodeCount == 0)
                {
                    continue;
                }

                var blogMlCategory = new BlogMLCategory
                {
                    Id = category.Id.ToString(),
                    CreatedOn = category.CreateDate,
                    LastModifiedOn = category.UpdateDate,
                    ApprovalStatus = BlogMLApprovalStatus.Approved,
                    ParentId = "0",
                    Title = new BlogMLTextConstruct(category.Text)
                };
                blogMlDoc.Categories.Add(blogMlCategory);
            }
        }

        private void AddBlogAuthors(IContent authorsNode, BlogMLDocument blogMlDoc)
        {
            foreach (IContent author in contentService.EnumeratePagedChildren(authorsNode.Id, 0, int.MaxValue, out _))
            {
                var blogMlAuthor = new BlogMLAuthor
                {
                    Id = author.Key.ToString(),
                    CreatedOn = author.CreateDate,
                    LastModifiedOn = author.UpdateDate,
                    ApprovalStatus = BlogMLApprovalStatus.Approved,
                    Title = new BlogMLTextConstruct(author.Name)
                };
                blogMlDoc.Authors.Add(blogMlAuthor);
            }
        }

        private void AddBlogPosts(
            IContent archiveNode,
            BlogMLDocument blogMlDoc,
            string? categoryGroup,
            string? tagGroup,
            bool exportImagesAsBase64)
        {
            const int pageSize = 1000;
            var pageIndex = 0;
            IContent[] posts;
            do
            {
                posts = contentService.EnumeratePagedChildren(
                    archiveNode.Id,
                    pageIndex,
                    pageSize,
                    out _,
                    ordering: Ordering.By("createDate")).ToArray();

                foreach (IContent child in posts)
                {
                    if (!child.Published)
                    {
                        continue;
                    }

                    ProcessSinglePost(child, blogMlDoc, categoryGroup, tagGroup, exportImagesAsBase64);
                }

                pageIndex++;
            }
            while (posts.Length == pageSize);
        }

        private void ProcessSinglePost(
            IContent child,
            BlogMLDocument blogMlDoc,
            string? categoryGroup,
            string? tagGroup,
            bool exportImagesAsBase64)
        {
            var content = GetPostContent(child);
            var postUrl = new Uri(urlProvider.GetUrl(child.Id), UriKind.RelativeOrAbsolute);
            var postAbsoluteUrl = new Uri(urlProvider.GetUrl(child.Id, UrlMode.Absolute), UriKind.Absolute);

            BlogMLPost blogMlPost = CreateBlogMlPost(child, content, postUrl);

            AssignAuthorToPost(blogMlPost, blogMlDoc, child.GetValue<string>("author"));
            AssignCategoriesToPost(blogMlPost, child, categoryGroup);
            AssignTagsToPost(blogMlPost, child, tagGroup);
            AttachPostImage(blogMlPost, child, postAbsoluteUrl, exportImagesAsBase64);

            _ = blogMlDoc.AddPost(blogMlPost);
        }

        private string GetPostContent(IContent child)
        {
            if (child.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateRichText))
            {
                // Prefer Umbraco's published value converter so configured RTE blocks render through
                // the same pipeline as the front end. Fall back to stored markup when unpublished.
                return richTextRenderer.GetRenderedMarkup(child);
            }

            if (child.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.ArticulateMarkdown))
            {
                // Markdown exports as sanitized HTML because BlogML content is HTML-oriented.
                // MDX/component syntax is intentionally unsupported by the current Markdown converter.
                var markdown = child.GetValue<string>("markdown");
                return markdown is not null ? articulateMarkdownConverter.ToHtml(markdown) : string.Empty;
            }

            return string.Empty;
        }

        private static BlogMLPost CreateBlogMlPost(IContent child, string content, Uri postUrl)
        {
            return new BlogMLPost
            {
                Id = child.Key.ToString(),
                Name = new BlogMLTextConstruct(child.Name),
                Title = new BlogMLTextConstruct(child.Name),
                ApprovalStatus = BlogMLApprovalStatus.Approved,
                PostType = BlogMLPostType.Normal,
                CreatedOn = child.CreateDate,
                LastModifiedOn = child.UpdateDate,
                Content = new BlogMLTextConstruct(content, BlogMLContentType.Html),
                Excerpt = new BlogMLTextConstruct(child.GetValue<string>("excerpt")),
                Url = postUrl
            };
        }

        private static void AssignAuthorToPost(BlogMLPost blogMlPost, BlogMLDocument blogMlDoc, string? authorName)
        {
            BlogMLAuthor? author = blogMlDoc.Authors?.FirstOrDefault(x =>
                x.Title is not null && x.Title.Content.InvariantEquals(authorName));

            if (author is not null)
            {
                blogMlPost.Authors.Add(author.Id);
            }
        }

        private void AssignCategoriesToPost(BlogMLPost blogMlPost, IContent child, string? categoryGroup)
        {
            IEnumerable<ITag> categories = tagService.GetTagsForEntity(child.Id, categoryGroup);
            foreach (ITag category in categories)
            {
                blogMlPost.Categories.Add(category.Id.ToString());
            }
        }

        private void AssignTagsToPost(BlogMLPost blogMlPost, IContent child, string? tagGroup)
        {
            var tags = tagService.GetTagsForEntity(child.Id, tagGroup).Select(t => t.Text).ToList();
            if (tags.Count > 0)
            {
                _ = blogMlPost.AddExtension(new TagsSyndicationExtension
                {
                    Context = { Tags = new Collection<string>(tags) }
                });
            }
        }

        private void AttachPostImage(
            BlogMLPost blogMlPost,
            IContent child,
            Uri postAbsoluteUrl,
            bool exportImagesAsBase64)
        {
            _ = TryExtractImage(exportImagesAsBase64, child, postAbsoluteUrl, blogMlPost);
        }

        private bool TryExtractImage(
            bool exportImagesAsBase64,
            IContent child,
            Uri postAbsoluteUrl,
            BlogMLPost blogMlPost)
        {
            if (!TryGetMediaId(child, out Guid mediaId))
            {
                return false;
            }

            IMedia? media = mediaService.GetById(mediaId);
            if (media is null)
            {
                logger.LogWarning(
                    "Post '{PostName}' (Id: {PostId}) references Media {MediaId} which could not be found in the database.",
                    child.Name,
                    child.Id,
                    mediaId);
                return false;
            }

            if (!TryGetMediaPath(media, out string? mediaPath))
            {
                logger.LogWarning(
                    "Post '{PostName}' (Id: {PostId}) references Media {MediaId} ('{MediaName}') but its file path could not be resolved.",
                    child.Name,
                    child.Id,
                    mediaId,
                    media.Name);
                return false;
            }

            var mime = mediaPath!.GetImageMimeType();
            if (string.IsNullOrWhiteSpace(mime))
            {
                logger.LogWarning(
                    "Post '{PostName}' (Id: {PostId}) references Media {MediaId} ('{MediaName}') at path '{MediaPath}' but MIME type could not be determined.",
                    child.Name,
                    child.Id,
                    mediaId,
                    media.Name,
                    mediaPath);
                return false;
            }

            try
            {
                BlogMLAttachment attachment =
                    CreateAttachmentFromMedia(exportImagesAsBase64, media, mediaPath!, mime, postAbsoluteUrl);
                blogMlPost.Attachments.Add(attachment);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Could not add the file to the blogML post attachments for Post '{PostName}' (Id: {PostId}).",
                    child.Name,
                    child.Id);
                return false;
            }
        }

        // Handles both V1 and V3 media formats
        private bool TryGetMediaId(IContent content, out Guid mediaId)
        {
            mediaId = Guid.Empty;
            if (!content.HasProperty("postImage"))
            {
                return false;
            }

            var value = content.GetValue<string>("postImage");
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.DetectIsJson())
            {
                try
                {
                    using var doc = JsonDocument.Parse(value);
                    JsonElement firstElement = doc.RootElement.EnumerateArray().FirstOrDefault();
                    if (firstElement.ValueKind != JsonValueKind.Undefined
                        && firstElement.TryGetProperty("mediaKey", out JsonElement mediaKeyElement)
                        && Guid.TryParse(mediaKeyElement.GetString(), out mediaId))
                    {
                        return true;
                    }
                }
                catch (JsonException)
                {
                    // Ignore and fall through to try other formats
                    logger.LogDebug("Failed to parse JSON for media value '{MediaValue}', falling back to UDI.", value);
                }
            }

            if (UdiParser.TryParse(value, out Udi? udi) && udi is GuidUdi guidUdi)
            {
                mediaId = guidUdi.Guid;
                return true;
            }

            return false;
        }

        // Handles both V1 and V3 media formats
        private bool TryGetMediaPath(IMedia media, out string? mediaPath)
        {
            var rawValue = media.GetValue<string>(Constants.Conventions.Media.File);
            if (rawValue is null)
            {
                mediaPath = null;
                return false;
            }

            if (rawValue.DetectIsJson())
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawValue);
                    if (doc.RootElement.TryGetProperty("src", out JsonElement src))
                    {
                        mediaPath = src.GetString();
                        return !string.IsNullOrWhiteSpace(mediaPath);
                    }
                }
                catch (JsonException)
                {
                    // Ignore, fall back to standard path handling
                    logger.LogDebug("Failed to parse JSON for media value '{MediaValue}', falling back to standard path handling.", rawValue);
                }
            }

            mediaPath = mediaFileManager.GetMediaPath(
                rawValue,
                media.Key,
                media.Properties[Constants.Conventions.Media.File]!.PropertyType.Key);

            return !string.IsNullOrWhiteSpace(mediaPath);
        }

        private BlogMLAttachment CreateAttachmentFromMedia(
            bool exportImagesAsBase64,
            IMedia media,
            string mediaFilePath,
            string mimeType,
            Uri postAbsoluteUrl)
        {
            var imageUrl =
                new Uri(
                    postAbsoluteUrl.GetLeftPart(UriPartial.Authority) + mediaFilePath.EnsureStartsWith('/'),
                    UriKind.Absolute);
            var attachment = new BlogMLAttachment
            {
                Url = imageUrl, ExternalUri = imageUrl, IsEmbedded = exportImagesAsBase64, MimeType = mimeType
            };

            if (exportImagesAsBase64)
            {
                using Stream mediaFileStream = mediaFileManager.GetFile(media, out _);
                using var memoryStream = new MemoryStream();
                mediaFileStream.CopyTo(memoryStream);
                attachment.Content = Convert.ToBase64String(memoryStream.ToArray());
            }
            else
            {
                attachment.Content = string.Empty;
            }

            return attachment;
        }
    }
}
