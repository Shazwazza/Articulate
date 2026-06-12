#nullable enable
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Argotic.Syndication.Specialized;
using Articulate.Services;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Task = System.Threading.Tasks.Task;

namespace Articulate.ImportExport
{
    /// <summary>
    /// Importer for blog content from BlogML format.
    /// </summary>
    public class BlogMlImporter(
        DisqusXmlExporter disqusXmlExporter,
        IContentService contentService,
        IContentTypeService contentTypeService,
        IUserService userService,
        ILogger<BlogMlImporter> logger,
        IDataTypeService dataTypeService,
        ISqlContext sqlContext,
        IScopeProvider scopeProvider,
        ILanguageService languageService,
        PropertyEditorCollection dataEditors,
        IJsonSerializer jsonSerializer,
        ArticulateTempFileSystem articulateTempFileSystem,
        IArticulateImportMediaService service,
        IHtmlSanitizer htmlSanitizer
#if UMBRACO_18_OR_GREATER
        , IIdKeyMap idKeyMap
#endif
        )
    {
        private const long MaxXmlCharacters = 10_000_000;

        internal int GetPostCount(string fileName) => GetDocument(fileName).Posts.Count();

        internal BlogMlImportFileSummary GetImportFileSummary(string fileName)
        {
            BlogMLDocument document = GetDocument(fileName);

            string[] externalHosts = document.Posts
                .SelectMany(post => post.Attachments)
                .Where(attachment => attachment.ExternalUri is not null && attachment.ExternalUri.IsAbsoluteUri)
                .Select(attachment => attachment.ExternalUri!.Host)
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(host => host, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            int externalImageCount = document.Posts
                .SelectMany(post => post.Attachments)
                .Count(attachment => attachment.ExternalUri is not null && attachment.ExternalUri.IsAbsoluteUri);

            return new BlogMlImportFileSummary(document.Posts.Count(), externalImageCount, externalHosts);
        }

        /// <summary>
        /// Imports the blog content from a BlogML file.
        /// </summary>
        /// <param name="userId">The ID of the user performing the import.</param>
        /// <param name="fileName">The name of the BlogML file in the temporary file system.</param>
        /// <param name="blogRootNode">The ID of the Articulate root node to import into.</param>
        /// <param name="overwrite">If true, existing posts are overwritten.</param>
        /// <param name="regexMatch">The regex pattern to match in post content.</param>
        /// <param name="regexReplace">The replacement string for the regex match.</param>
        /// <param name="publishAll">If true, all imported posts are published.</param>
        /// <param name="exportDisqusXml">If true, an XML file for Disqus import is generated.</param>
        /// <param name="importFirstImage">If true, the first image in each post is extracted to a property.</param>
        /// <returns>An <see cref="ImportResponseDto"/> containing import statistics.</returns>
        internal async Task<ImportResponseDto> ImportAsync(
            int userId,
            string fileName,
            Guid blogRootNode,
            bool overwrite,
            string? regexMatch,
            string? regexReplace,
            bool publishAll,
            bool exportDisqusXml = false,
            bool importFirstImage = false)
        {
            // not inside try block because we don't want to proceed further, and caller should handle
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("Filename is required");
            }

            if (!articulateTempFileSystem.FileExists(fileName))
            {
                throw new FileNotFoundException("File not found: " + fileName);
            }

            IContent root = contentService.GetById(blogRootNode)
                            ?? throw new InvalidOperationException("No node found with id " + blogRootNode);

            if (!root.ContentType.Alias.InvariantEquals(ArticulateConstants.ContentType.Articulate))
            {
                throw new InvalidOperationException("The node with id " + blogRootNode +
                                                    " is not an Articulate root node");
            }

            // wrap entire operation in scope
            using IScope scope = scopeProvider.CreateScope();
            var returnModel = new ImportResponseDto();

            BlogMLDocument document = GetDocument(fileName);
            XDocument xDoc = LoadBlogMlXDocument(fileName);

            Dictionary<string, string> authorIdsToName =
                await ImportAuthorsAsync(userId, root, document.Authors);
            returnModel.AuthorCount = authorIdsToName.Count;

            IEnumerable<IContent> imported = await ImportPostsAsync(
                userId,
                xDoc,
                root,
                document.Posts,
                [.. document.Authors],
                [.. document.Categories],
                authorIdsToName,
                overwrite,
                regexMatch,
                regexReplace,
                publishAll,
                importFirstImage);
            IContent[] enumerable = imported as IContent[] ?? [.. imported];
            returnModel.PostCount = enumerable.Length;

            if (exportDisqusXml)
            {
                XDocument xDisqusDoc = disqusXmlExporter.Export(enumerable, document);
                const string nsWp = "http://wordpress.org/export/1.0/";
                returnModel.CommentCount = xDisqusDoc.Descendants(XName.Get("comment", nsWp)).Count();
                using var memStream = new MemoryStream();
                xDisqusDoc.Save(memStream);
                var disqusFileName = $"DisqusXmlExport-{userId}.xml";
                articulateTempFileSystem.AddFile(disqusFileName, memStream, true);
            }

            // commit
            _ = scope.Complete();
            returnModel.Completed = true;
            return returnModel;
        }

        private BlogMLDocument GetDocument(string fileName)
        {
            if (!articulateTempFileSystem.FileExists(fileName))
            {
                throw new FileNotFoundException("File not found: " + fileName);
            }

            using Stream stream = articulateTempFileSystem.OpenFile(fileName);
            try
            {
                using XmlReader reader = CreateSecureXmlReader(stream);
                var document = new BlogMLDocument();
                document.Load(reader);
                return document;
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException("The BlogML file contains invalid XML.", ex);
            }
        }

        private XDocument LoadBlogMlXDocument(string fileName)
        {
            using Stream stream = articulateTempFileSystem.OpenFile(fileName);
            try
            {
                using XmlReader reader = CreateSecureXmlReader(stream);
                return XDocument.Load(reader, LoadOptions.None);
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException("The BlogML file contains invalid XML.", ex);
            }
        }

        private static XmlReader CreateSecureXmlReader(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxXmlCharacters,
                MaxCharactersFromEntities = 1024,
            };

            return XmlReader.Create(stream, settings);
        }

        private async Task<Dictionary<string, string>> ImportAuthorsAsync(
            int userId,
            IContent rootNode,
            IEnumerable<BlogMLAuthor>? authors)
        {
            var result = new Dictionary<string, string>();

            if (authors is null)
            {
                return result;
            }

            IContentType authorType = contentTypeService.Get(ArticulateConstants.ContentType.ArticulateAuthor)
                                      ?? throw new InvalidOperationException(
                                          "Articulate is not installed properly, the 'ArticulateAuthor' doc type could not be found");

            IContentType authorsType = contentTypeService.Get(ArticulateConstants.ContentType.ArticulateAuthors)
                                       ?? throw new InvalidOperationException(
                                           "Articulate is not installed properly, the 'ArticulateAuthors' doc type could not be found");

            IContent authorsNode =
                await GetOrCreateAuthorsContainerAsync(userId, rootNode, authorsType);
            IContent[] existingAuthorNodes = GetExistingAuthorNodes(authorsNode.Id, authorType.Id);

            foreach (BlogMLAuthor author in authors)
            {
                var authorName = await ProcessSingleAuthorAsync(
                    userId,
                    author,
                    authorsNode,
                    authorType,
                    existingAuthorNodes);
                result.Add(author.Id, authorName);
            }

            return result;
        }

        private async Task<IContent> GetOrCreateAuthorsContainerAsync(
            int userId,
            IContent rootNode,
            IContentType authorsType)
        {
            IEnumerable<IContent> allAuthorsNodes = contentService.GetPagedOfType(
                authorsType.Id,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.ParentId == rootNode.Id && x.Trashed == false));

            IContent? authorsNode = allAuthorsNodes.FirstOrDefault();
            if (authorsNode is not null)
            {
                return authorsNode;
            }

            authorsNode = await contentService.CreateWithInvariantOrDefaultCultureNameAsync(
                ArticulateConstants.Convention.AuthorsDocument,
                rootNode,
                authorsType,
                languageService,
                logger);

            OperationResult authorsSaveResult = contentService.Save(authorsNode, userId: userId);
            authorsSaveResult.EnsureSuccess(logger, $"save authors container {authorsNode.Id}");

            PublishResult authorsPublishResult = contentService.Publish(authorsNode, ["*"], userId: userId);
            authorsPublishResult.EnsureSuccess(logger, $"publish authors container {authorsNode.Id}");

            return authorsNode;
        }

        private IContent[] GetExistingAuthorNodes(int authorsNodeId, int authorTypeId)
        {
            IEnumerable<IContent> allAuthorNodes = contentService.GetPagedOfType(
                authorTypeId,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.ParentId == authorsNodeId && x.Trashed == false));

            return allAuthorNodes as IContent[] ?? [.. allAuthorNodes];
        }

        private async Task<string> ProcessSingleAuthorAsync(
            int userId,
            BlogMLAuthor author,
            IContent authorsNode,
            IContentType authorType,
            IContent[] existingAuthorNodes)
        {
            IUser? found = userService.GetByEmail(author.EmailAddress);
            var authorName = found?.Name ?? author.Title.Content;

            IContent authorNode = existingAuthorNodes.FirstOrDefault(x => x.Name.InvariantEquals(authorName)) ??
                                  await CreateAndPublishAuthorNodeAsync(userId, authorName, authorsNode, authorType);

            return authorNode.Name!;
        }

        private async Task<IContent> CreateAndPublishAuthorNodeAsync(
            int userId,
            string authorName,
            IContent authorsNode,
            IContentType authorType)
        {
            IContent authorNode = await contentService.CreateWithInvariantOrDefaultCultureNameAsync(
                authorName,
                authorsNode,
                authorType,
                languageService,
                logger);

            OperationResult authorSaveResult = contentService.Save(authorNode, userId: userId);
            authorSaveResult.EnsureSuccess(logger, $"save author {authorNode.Name}");

            PublishResult authorPublishResult = contentService.Publish(authorNode, ["*"], userId: userId);
            authorPublishResult.EnsureSuccess(logger, $"publish author {authorNode.Name}");

            return authorNode;
        }

        private async Task<IEnumerable<IContent>> ImportPostsAsync(
            int userId,
            XDocument xDoc,
            IContent rootNode,
            IEnumerable<BlogMLPost> posts,
            BlogMLAuthor[] authors,
            BlogMLCategory[] categories,
            Dictionary<string, string> authorIdsToName,
            bool overwrite,
            string? regexMatch,
            string? regexReplace,
            bool publishAll,
            bool importFirstImage = false)
        {
            var result = new List<IContent>();

            IContentType postType = contentTypeService.Get(ArticulateConstants.ContentType.ArticulateRichText)
                                    ?? throw new InvalidOperationException(
                                        "Articulate is not installed properly, the 'ArticulateRichText' doc type could not be found");

            IContent archiveNode = await GetOrCreateArchiveNodeAsync(userId, rootNode);
            IContent[] existingPosts = GetExistingPosts(archiveNode);

            foreach (BlogMLPost post in posts)
            {
                IContent? postNode = FindExistingPost(existingPosts, post);

                // Skip if exists and we don't want to overwrite
                if (!overwrite && postNode is not null)
                {
                    continue;
                }

                // Create if it doesn't exist
                if (postNode is null)
                {
                    var title = WebUtility.HtmlDecode(post.Title.Content);
                    postNode = await contentService
                        .CreateWithInvariantOrDefaultCultureNameAsync(
                            title,
                            archiveNode,
                            postType,
                            languageService,
                            logger);
                }

                await PopulatePostContentAsync(postNode, postType, post, regexMatch, regexReplace);
                await SetPostMetadataAsync(postNode, postType, post, xDoc, authors, categories, authorIdsToName);

                if (importFirstImage)
                {
                    await ImportFirstImageAsync(postNode, postType, post);
                }

                SaveAndPublishPost(postNode, userId, publishAll);
                result.Add(postNode);
            }

            return result;
        }

        private async Task ImportFirstImageAsync(IContentBase postNode, IContentType postType, BlogMLPost post)
        {
            // Filter for image attachments (any image/* MIME type)
            BlogMLAttachment? attachment = post.Attachments.FirstOrDefault(p =>
                p.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);

            if (attachment is null)
            {
                return;
            }

            ImportMediaValidationResult validationResult;
            string attachmentSource;
            string attachmentIdentifier;

            // Decode/download and validate the image
            if (!attachment.Content.IsNullOrWhiteSpace())
            {
                // Base64 content
                var fileName = attachment.Url is not null
                    ? Path.GetFileName(attachment.Url.OriginalString)
                    : $"{post.Id}-image";
                attachmentSource = "base64";
                attachmentIdentifier = fileName;
                validationResult = await service
                    .DecodeAndValidateBase64ImageAsync(attachment.Content, fileName);
            }
            else if (attachment.ExternalUri is not null && attachment.ExternalUri.IsAbsoluteUri)
            {
                // External URL
                attachmentSource = "external URL";
                attachmentIdentifier = attachment.ExternalUri.ToString();
                validationResult = await service.DownloadAndValidateImageAsync(attachment.ExternalUri);
            }
            else
            {
                logger.LogWarning(
                    "BlogML attachment for post '{PostName}' (ImportId: {ImportId}) has neither base64 content nor external URL",
                    postNode.Name,
                    post.Id);
                return;
            }

            if (!validationResult.IsValid)
            {
                logger.LogWarning(
                    "BlogML attachment validation failed for post '{PostName}' (ImportId: {ImportId}, source: {Source}, identifier: {Identifier}): {ErrorMessage}",
                    postNode.Name,
                    post.Id,
                    attachmentSource,
                    attachmentIdentifier,
                    validationResult.ErrorMessage);
                return;
            }

            try
            {
                // Save to media library (service handles name cleaning/fallback)
                ImportMediaSaveResult saveResult = service.SaveToMediaLibrary(
                    validationResult.ValidatedStream!,
                    postNode.Name ?? $"Post-{post.Id}-image",
                    validationResult.CorrectExtension!,
                    service.GetOrCreateArticulateMediaFolder());

                if (!saveResult.Success)
                {
                    logger.LogWarning(
                        "Failed to save BlogML image for post '{PostName}' (ImportId: {ImportId}, source: {Source}, identifier: {Identifier}): {ErrorMessage}",
                        postNode.Name,
                        post.Id,
                        attachmentSource,
                        attachmentIdentifier,
                        saveResult.ErrorMessage);
                    return;
                }

                // Set the postImage property
                await postNode.SetInvariantOrDefaultCultureValueAsync(
                    "postImage",
                    saveResult.MediaUdi,
                    postType,
                    languageService,
                    logger);
            }
            catch (PathTooLongException ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not save image for post '{PostName}' (ImportId: {ImportId}) due to path length",
                    postNode.Name,
                    post.Id);
            }
            finally
            {
                if (validationResult.ValidatedStream is not null)
                {
                    await validationResult.ValidatedStream.DisposeAsync();
                }
            }
        }

        /* private async Task ImportComments(int userId, IContent postNode, BlogMLPost post,
        //    string publicKey, string privateKey, string accessToken)
        // {
        //    var importer = new DisqusImporter(publicKey);
        //    foreach (var comment in post.Comments)
        //    {
        //        var result = await importer.Import(
        //            postNode.Id.ToString(CultureInfo.InvariantCulture),
        //            comment.Content.Content,
        //            comment.UserName,
        //            comment.UserEmailAddress,
        //            comment.UserUrl is not null ? comment.UserUrl.ToString() : string.Empty,
        //            comment.CreatedOn);
        //        if (!result)
        //        {
        //            HasErrors = true;
        //        }
        //        else
        //        {
        //            postNode.SetInvariantOrDefaultLanguageValue("disqusCommentsImported", 1);
        //            //just save it, we don't need to publish it (if publish = true then its already published), we just need
        //            // this for reference.
        //            _applicationContext.Services.ContentService.Save(postNode, userId);
        //        }
        //    }
        // } */

        private Task ImportCategoriesAsync(
            IContent postNode,
            BlogMLPost post,
            IEnumerable<BlogMLCategory> allCategories,
            IContentType postType)
        {
            var postCats = allCategories.Where(x => post.Categories.Contains(x.Id))
                .Select(x => x.Title.Content)
                .ToArray();

            return postNode.AssignInvariantOrDefaultCultureTagsAsync(
                "categories",
                postCats,
                postType,
                languageService,
                dataTypeService,
                dataEditors,
                jsonSerializer,
#if UMBRACO_18_OR_GREATER
                idKeyMap,
#endif
                logger);
        }

        private async Task ImportTagsAsync(XDocument xDoc, IContent postNode, BlogMLPost post, IContentType postType)
        {
            if (xDoc.Root is null)
            {
                return;
            }

            // since this blobml serializer doesn't support tags (can't find one that does) we need to manually take care of that
            XElement? xmlPost = xDoc.Descendants(XName.Get("post", xDoc.Root.Name.NamespaceName))
                .SingleOrDefault(x => x.Attribute("id")?.Value.ToString() == post.Id);

            xmlPost ??= xDoc.Descendants(XName.Get("post", xDoc.Root.Name.NamespaceName))
                .SingleOrDefault(x => x.Descendants(XName.Get("post-name", xDoc.Root.Name.NamespaceName))
                    .SingleOrDefault(s => s.Value == post.Name.Content) is not null);

            if (xmlPost is null)
            {
                return;
            }

            var tags = xmlPost.Descendants(XName.Get("tag", xDoc.Root.Name.NamespaceName))
                .Select(x => x.Attribute("ref")?.Value)
                .Where(x => x is not null)
                .OfType<string>()
                .ToArray();

            await postNode.AssignInvariantOrDefaultCultureTagsAsync(
                "tags",
                tags,
                postType,
                languageService,
                dataTypeService,
                dataEditors,
                jsonSerializer,
#if UMBRACO_18_OR_GREATER
                idKeyMap,
#endif
                logger);
        }

        private async Task<IContent> GetOrCreateArchiveNodeAsync(int userId, IContent rootNode)
        {
            IContentType archiveDocType = contentTypeService.Get(ArticulateConstants.ContentType.ArticulateArchive)
                                          ?? throw new InvalidOperationException(
                                              "Articulate is not installed properly, the 'ArticulateArchive' doc type could not be found");

            IEnumerable<IContent> archive = contentService.GetPagedOfType(
                archiveDocType.Id,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.ParentId == rootNode.Id && x.Trashed == false));

            IContent? archiveNode = archive.FirstOrDefault();

            if (archiveNode is null)
            {
                archiveNode = await contentService.CreateWithInvariantOrDefaultCultureNameAsync(
                    ArticulateConstants.Convention.ArticlesDocument,
                    rootNode,
                    archiveDocType,
                    languageService,
                    logger);

                OperationResult archiveSaveResult = contentService.Save(archiveNode, userId);
                archiveSaveResult.EnsureSuccess(logger, $"save archive container {archiveNode.Id}");
            }

            return archiveNode;
        }

        private IContent[] GetExistingPosts(IContent archiveNode)
        {
            IEnumerable<IContent> allPostNodes = contentService.GetPagedChildrenCompat(
                archiveNode.Id,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.ParentId == archiveNode.Id && x.Trashed == false));

            return allPostNodes as IContent[] ?? [.. allPostNodes];
        }

        private static IContent? FindExistingPost(IContent[] existingPosts, BlogMLPost post)
        {
            if (!string.IsNullOrWhiteSpace(post.Id))
            {
                return existingPosts.FirstOrDefault(x => x.GetValue<string>("importId") == post.Id);
            }

            return existingPosts
                .Select(x => new { Node = x, UrlName = x.GetValue<string>(Constants.Conventions.Content.UrlName) })
                .Where(x => x.UrlName is not null && post.Name != null &&
                            x.UrlName.InvariantStartsWith(post.Name.Content))
                .Select(x => x.Node)
                .FirstOrDefault();
        }

        private async Task PopulatePostContentAsync(
            IContentBase postNode,
            IContentType postType,
            BlogMLPost post,
            string? regexMatch,
            string? regexReplace)
        {
            await postNode
                .SetInvariantOrDefaultCultureValueAsync(
                    "publishedDate",
                    post.CreatedOn,
                    postType,
                    languageService,
                    logger);

            if (post.Excerpt is not null && !post.Excerpt.Content.IsNullOrWhiteSpace())
            {
                var excerpt = DecodeContent(post.Excerpt.Content, post.Excerpt.ContentType);

                await postNode.SetInvariantOrDefaultCultureValueAsync(
                    "excerpt",
                    excerpt,
                    postType,
                    languageService,
                    logger);
            }

            await postNode.SetInvariantOrDefaultCultureValueAsync(
                "importId",
                post.Id,
                postType,
                languageService,
                logger);

            var content = DecodeContent(post.Content.Content, post.Content.ContentType);
            content = ApplyImportRegex(content, regexMatch, regexReplace);
            content = htmlSanitizer.Sanitize(content);

            await postNode
                .SetInvariantOrDefaultCultureValueAsync(
                    "richText",
                    new HtmlString(content),
                    postType,
                    languageService,
                    logger);
            await postNode
                .SetInvariantOrDefaultCultureValueAsync(
                    "enableComments",
                    true,
                    postType,
                    languageService,
                    logger);

            await SetPostSlugAsync(postNode, postType, post);
        }

        private static string DecodeContent(string content, BlogMLContentType contentType) =>
            contentType == BlogMLContentType.Base64
                ? Encoding.UTF8.GetString(Convert.FromBase64String(content))
                : content;

        private string ApplyImportRegex(string content, string? regexMatch, string? regexReplace)
        {
            if (regexMatch.IsNullOrWhiteSpace() || regexReplace.IsNullOrWhiteSpace())
            {
                return content;
            }

            try
            {
                return Regex.Replace(
                    content,
                    regexMatch,
                    regexReplace,
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid regex pattern provided during import: {RegexMatch}", regexMatch);
                throw new InvalidOperationException($"The provided regex pattern '{regexMatch}' is invalid.", ex);
            }
            catch (RegexMatchTimeoutException ex)
            {
                logger.LogWarning(ex, "Regex operation timed out during import for pattern: {RegexMatch}", regexMatch);
                throw new InvalidOperationException("The regex operation timed out. The pattern might be too complex.", ex);
            }
        }

        private async Task SetPostSlugAsync(IContentBase postNode, IContentType postType, BlogMLPost post)
        {
            if (post.Url is null || string.IsNullOrWhiteSpace(post.Url.OriginalString))
            {
                return;
            }

            string slug = ExtractSlugFromPost(post);
            await postNode.SetInvariantOrDefaultCultureValueAsync(
                Constants.Conventions.Content.UrlName,
                slug,
                postType,
                languageService,
                logger);
        }

        private static string ExtractSlugFromPost(BlogMLPost post)
        {
            if (post.Name is not null)
            {
                return post.Name.Content;
            }

            var slugArray = post.Url!.OriginalString.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            var fileNameAndQuery = slugArray[^1];
            var fileNameAndQueryArray = fileNameAndQuery.Split(['?'], StringSplitOptions.RemoveEmptyEntries);
            var fileName = fileNameAndQueryArray[0];
            int lastDotIndex = fileName.LastIndexOf('.');
            return lastDotIndex > 0 ? fileName[..lastDotIndex] : fileName;
        }

        private async Task SetPostMetadataAsync(
            IContent postNode,
            IContentType postType,
            BlogMLPost post,
            XDocument xDoc,
            BlogMLAuthor[] authors,
            BlogMLCategory[] categories,
            Dictionary<string, string> authorIdsToName)
        {
            if (post.Authors.Count > 0)
            {
                BlogMLAuthor? author = authors.FirstOrDefault(x => x.Id.InvariantEquals(post.Authors[0]));
                if (author is not null && authorIdsToName.TryGetValue(author.Id, out string? name))
                {
                    await postNode
                        .SetInvariantOrDefaultCultureValueAsync("author", name, postType, languageService, logger);
                }
            }

            await ImportTagsAsync(xDoc, postNode, post, postType);
            await ImportCategoriesAsync(postNode, post, categories, postType);
        }

        private void SaveAndPublishPost(IContent postNode, int userId, bool publishAll)
        {
            if (publishAll)
            {
                OperationResult saveResult = contentService.Save(postNode, userId: userId);
                saveResult.EnsureSuccess(logger, $"save post {postNode.Id}");

                PublishResult publishResult = contentService.Publish(postNode, ["*"], userId);
                publishResult.EnsureSuccess(logger, $"publish post {postNode.Id}");
            }
            else
            {
                OperationResult saveResult = contentService.Save(postNode, userId);
                saveResult.EnsureSuccess(logger, $"save post {postNode.Id}");
            }
        }
    }

    internal sealed record BlogMlImportFileSummary(int PostCount, int ExternalImageCount, string[] ExternalHosts);
}
