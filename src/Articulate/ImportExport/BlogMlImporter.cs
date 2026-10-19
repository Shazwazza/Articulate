#nullable enable
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Argotic.Syndication.Specialized;
using Articulate.Options;
using Articulate.Services;
using Articulate.Syndication.BlogML;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
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
        IHtmlSanitizer htmlSanitizer,
        IShortStringHelper shortStringHelper,
        IOptions<ArticulateOptions> articulateOptions,
        IOptions<ArticulateCommentsOptions> commentsOptions
#if UMBRACO_18_OR_GREATER
        ,
        IIdKeyMap idKeyMap
#endif
    )
    {
        private readonly long _maxXmlCharacters = articulateOptions.Value.BlogMlImportMaxXmlCharacters;

        private readonly ArticulateCommentsOptions _commentsOptions = commentsOptions.Value;

        internal BlogMlImportFileSummary GetImportFileSummary(string fileName)
        {
            BlogMLDocument document = GetDocument(fileName);

            Uri[] externalUris = document.Posts
                .SelectMany(post => post.Attachments)
                .Select(attachment => attachment.ExternalUri)
                .OfType<Uri>()
                .Where(uri => uri.IsAbsoluteUri)
                .ToArray();

            string[] externalHosts = externalUris
                .Select(uri => uri.Host)
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(host => host, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new BlogMlImportFileSummary(document.Posts.Count(), externalUris.Length, externalHosts);
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

            XDocument xDoc = LoadBlogMlXDocument(fileName);
            BlogMLDocument document = ToBlogMlDocument(xDoc);

            Dictionary<string, string> authorIdsToName =
                await ImportAuthorsAsync(userId, root, document.Authors);
            returnModel.AuthorCount = authorIdsToName.Count;

            IEnumerable<IContent> imported = await ImportPostsAsync(
                userId,
                xDoc,
                root,
                document.Posts,
                [.. document.Categories],
                authorIdsToName,
                overwrite,
                regexMatch,
                regexReplace,
                publishAll,
                importFirstImage);
            IContent[] enumerable = imported as IContent[] ?? [.. imported];
            returnModel.PostCount = enumerable.Length;

            // Warn when BlogML has comments but Giscus is configured and Disqus export isn't requested.
            // Giscus has no import endpoint, so comments would be silently discarded otherwise.
            if (!exportDisqusXml && _commentsOptions.Giscus.IsFullyConfigured())
            {
                int postsWithComments = 0, totalComments = 0;
                foreach (BlogMLPost post in document.Posts)
                {
                    postsWithComments += post.Comments.Count > 0 ? 1 : 0;
                    totalComments += post.Comments.Count;
                }

                if (totalComments > 0)
                {
                    logger.LogWarning(
                        "BlogML import contains {CommentCount} comment(s) across {PostCount} post(s), but Giscus is configured and no Disqus XML export was requested. Giscus has no import endpoint; these comments will not be migrated. See https://github.com/Shazwazza/Articulate/wiki/Comments#caveats for migration options.",
                        totalComments,
                        postsWithComments);
                }
            }

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

        private BlogMLDocument GetDocument(string fileName) => ToBlogMlDocument(LoadBlogMlXDocument(fileName));

        internal static BlogMLDocument ToBlogMlDocument(XDocument xDoc)
        {
            using XmlReader reader = xDoc.CreateReader();
            var document = new BlogMLDocument();
            document.Load(reader);
            return document;
        }

        private XDocument LoadBlogMlXDocument(string fileName)
        {
            if (!articulateTempFileSystem.FileExists(fileName))
            {
                throw new FileNotFoundException("File not found: " + fileName);
            }

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

        private XmlReader CreateSecureXmlReader(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = _maxXmlCharacters,
            };

            return XmlReader.Create(stream, settings);
        }

        private async Task<Dictionary<string, string>> ImportAuthorsAsync(
            int userId,
            IContent rootNode,
            IEnumerable<BlogMLAuthor>? authors)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            Dictionary<string, IContent> authorNodesByName = GetExistingAuthorNodes(authorsNode.Id, authorType.Id);

            foreach (BlogMLAuthor author in authors)
            {
                var authorName = await ProcessSingleAuthorAsync(
                    userId,
                    author,
                    authorsNode,
                    authorType,
                    authorNodesByName);
                if (authorName is not null)
                {
                    _ = result.TryAdd(author.Id, authorName);
                }
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

        private Dictionary<string, IContent> GetExistingAuthorNodes(int authorsNodeId, int authorTypeId)
        {
            IEnumerable<IContent> allAuthorNodes = contentService.GetPagedOfType(
                authorTypeId,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.ParentId == authorsNodeId && x.Trashed == false));

            var byName = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase);
            foreach (IContent node in allAuthorNodes)
            {
                if (node.Name is not null)
                {
                    _ = byName.TryAdd(node.Name, node);
                }
            }

            return byName;
        }

        internal async Task<string?> ProcessSingleAuthorAsync(
            int userId,
            BlogMLAuthor author,
            IContent authorsNode,
            IContentType authorType,
            Dictionary<string, IContent> authorNodesByName)
        {
            IUser? found = userService.GetByEmail(author.EmailAddress);
            string? authorName = found?.Name ?? author.Title?.Content;
            if (string.IsNullOrWhiteSpace(authorName))
            {
                authorName = author.Id;
            }

            if (string.IsNullOrWhiteSpace(authorName))
            {
                return null;
            }

            if (!authorNodesByName.TryGetValue(authorName, out IContent? authorNode))
            {
                authorNode = await CreateAndPublishAuthorNodeAsync(userId, authorName, authorsNode, authorType);
                authorNodesByName[authorName] = authorNode;
            }

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

            IContentType archiveType = contentTypeService.Get(ArticulateConstants.ContentType.ArticulateArchive)
                                       ?? throw new InvalidOperationException(
                                           "Articulate is not installed properly, the 'ArticulateArchive' doc type could not be found");

            List<IContent> archives = [.. GetArchiveNodes(rootNode, archiveType)];

            var createdArchives = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase);
            var existingPostsByArchive = new Dictionary<Guid, (Dictionary<string, IContent> BySlug, Dictionary<string, IContent> ByImportId, Dictionary<string, int> TakenSlugs)>();
            var postsByImportId = new Dictionary<string, IContent>(StringComparer.Ordinal);
            string? urlNameCulture = await ContentExtensions.GetInvariantOrDefaultCultureAsync(
                Constants.Conventions.Content.UrlName,
                postType,
                languageService,
                logger);

            (Dictionary<string, XElement> ById, Dictionary<string, XElement> ByName) postElements = BuildPostElementLookup(xDoc);

            (Dictionary<string, IContent> BySlug, Dictionary<string, IContent> ByImportId, Dictionary<string, int> TakenSlugs) GetArchivePosts(IContent archive)
            {
                if (!existingPostsByArchive.TryGetValue(archive.Key, out (Dictionary<string, IContent> BySlug, Dictionary<string, IContent> ByImportId, Dictionary<string, int> TakenSlugs) cached))
                {
                    var bySlug = new Dictionary<string, IContent>(StringComparer.OrdinalIgnoreCase);
                    var byImportId = new Dictionary<string, IContent>(StringComparer.Ordinal);
                    var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (IContent existing in GetExistingPosts(archive))
                    {
                        string? slug = SlugSegment(existing, shortStringHelper, urlNameCulture);
                        if (!string.IsNullOrEmpty(slug))
                        {
                            _ = bySlug.TryAdd(slug, existing);
                        }

                        TakeSlug(taken, slug);
                        if (existing.GetValue<string>("importId") is { Length: > 0 } importId)
                        {
                            _ = byImportId.TryAdd(importId, existing);
                            _ = postsByImportId.TryAdd(importId, existing);
                        }
                    }

                    cached = (bySlug, byImportId, taken);
                    existingPostsByArchive[archive.Key] = cached;
                }

                return cached;
            }

            // Marked posts match importId root-wide, so every archive is indexed up front
            foreach (IContent archive in archives)
            {
                _ = GetArchivePosts(archive);
            }

            foreach (BlogMLPost post in posts)
            {
                XElement? postElement = FindPostElement(postElements, post);
                var marker = new ArchiveSyndicationExtension();
                ArchiveSyndicationExtension? archiveMarker = postElement is not null && marker.Load(postElement.CreateNavigator()) ? marker : null;

                IContent archiveNode = ResolveArchiveForPost(rootNode, archives, archiveMarker);
                if (archiveNode.Key == rootNode.Key)
                {
                    archiveNode = await GetOrCreateArchiveNodeAsync(createdArchives, userId, rootNode, archiveMarker, publishAll, archiveType);
                    if (!archives.Contains(archiveNode))
                    {
                        archives.Add(archiveNode);
                    }
                }

                (Dictionary<string, IContent> BySlug, Dictionary<string, IContent> ByImportId, Dictionary<string, int> TakenSlugs) archivePosts = GetArchivePosts(archiveNode);

                // Only Articulate exports (GUID ids) match root-wide; other sources may reuse small ids across blogs
                IContent? postNode = FindExistingPost(archiveMarker is null ? archivePosts.ByImportId : postsByImportId, archivePosts.BySlug, post, shortStringHelper);

                // An existing post stays in the archive it already lives in, whatever the marker says
                if (postNode is not null && archives.FirstOrDefault(archive => archive.Id == postNode.ParentId) is { } owner && owner.Key != archiveNode.Key)
                {
                    logger.LogWarning(
                        "BlogML post '{PostTitle}' (ImportId: {ImportId}) matched existing post '{PostName}' in archive '{OwnerArchive}' rather than '{TargetArchive}'; if the file is from a different source its ids may collide",
                        post.Title.Content,
                        post.Id,
                        postNode.Name,
                        owner.Name,
                        archiveNode.Name);
                    archiveNode = owner;
                    archivePosts = GetArchivePosts(owner);
                }

                // Skip if exists and we don't want to overwrite
                if (!overwrite && postNode is not null)
                {
                    continue;
                }

                if (postNode is not null)
                {
                    ReleaseSlug(archivePosts.TakenSlugs, SlugSegment(postNode, shortStringHelper, urlNameCulture));
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

                await PopulatePostContentAsync(postNode, postType, post, archivePosts.TakenSlugs, urlNameCulture, regexMatch, regexReplace);
                TakeSlug(archivePosts.TakenSlugs, SlugSegment(postNode, shortStringHelper, urlNameCulture));
                await SetPostMetadataAsync(postNode, postType, post, postElement, categories, authorIdsToName);

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

        private async Task ImportTagsAsync(XElement? xmlPost, IContent postNode, IContentType postType)
        {
            // since this blobml serializer doesn't support tags (can't find one that does) we need to manually take care of that
            if (xmlPost is null)
            {
                return;
            }

            var tags = xmlPost.Descendants(xmlPost.Name.Namespace + "tag")
                .Select(x => x.Attribute("ref")?.Value)
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

        private IContent[] GetArchiveNodes(IContent rootNode, IContentType archiveDocType)
        {
            IEnumerable<IContent> archives = contentService.GetPagedOfType(
                archiveDocType.Id,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.ParentId == rootNode.Id && x.Trashed == false));

            return [.. archives];
        }

        internal async Task<IContent> GetOrCreateArchiveNodeAsync(
            Dictionary<string, IContent> createdArchives,
            int userId,
            IContent rootNode,
            ArchiveSyndicationExtension? archiveMarker,
            bool publishAll,
            IContentType archiveDocType)
        {
            string archiveName = archiveMarker?.ArchiveName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(archiveName))
            {
                archiveName = ArticulateConstants.Convention.ArticlesDocument;
            }

            if (!createdArchives.TryGetValue(archiveName, out IContent? created))
            {
                created = await CreateArchiveNodeAsync(userId, rootNode, archiveName, publishAll, archiveDocType);
                createdArchives[archiveName] = created;
            }

            return created;
        }

        internal async Task<IContent> CreateArchiveNodeAsync(int userId, IContent rootNode, string archiveName, bool publishAll, IContentType archiveDocType)
        {
            IContent archiveNode = await contentService.CreateWithInvariantOrDefaultCultureNameAsync(
                archiveName,
                rootNode,
                archiveDocType,
                languageService,
                logger);

            OperationResult archiveSaveResult = contentService.Save(archiveNode, userId);
            archiveSaveResult.EnsureSuccess(logger, $"save archive container {archiveNode.Id}");

            if (publishAll)
            {
                PublishResult archivePublishResult = contentService.Publish(archiveNode, ["*"], userId);
                archivePublishResult.EnsureSuccess(logger, $"publish archive container {archiveNode.Id}");
            }

            return archiveNode;
        }

        // Only the root's own archives are candidates so the XML can't target content elsewhere; returning root means "create it".
        internal static IContent ResolveArchiveForPost(IContent root, IReadOnlyList<IContent> archives, ArchiveSyndicationExtension? archiveMarker)
        {
            if (archiveMarker is not null && archiveMarker.ArchiveKey != Guid.Empty)
            {
                IContent? match = archives.FirstOrDefault(archive => archive.Key == archiveMarker.ArchiveKey);
                if (match is not null)
                {
                    return match;
                }
            }

            string? archiveName = archiveMarker?.ArchiveName?.Trim();
            if (!string.IsNullOrWhiteSpace(archiveName))
            {
                IContent? match = archives.FirstOrDefault(archive =>
                    string.Equals(archive.Name, archiveName, StringComparison.OrdinalIgnoreCase));

                return match ?? root;
            }

            return archives.FirstOrDefault() ?? root;
        }

        internal static (Dictionary<string, XElement> ById, Dictionary<string, XElement> ByName) BuildPostElementLookup(XDocument? xDoc)
        {
            var byId = new Dictionary<string, XElement>(StringComparer.Ordinal);
            var byName = new Dictionary<string, XElement>(StringComparer.Ordinal);
            if (xDoc?.Root is null)
            {
                return (byId, byName);
            }

            XNamespace ns = xDoc.Root.Name.Namespace;
            foreach (XElement xmlPost in xDoc.Descendants(ns + "post"))
            {
                if (xmlPost.Attribute("id")?.Value is { } id)
                {
                    _ = byId.TryAdd(id, xmlPost);
                }

                if (xmlPost.Element(ns + "post-name")?.Value is { } name)
                {
                    _ = byName.TryAdd(name, xmlPost);
                }
            }

            return (byId, byName);
        }

        internal static XElement? FindPostElement(
            (Dictionary<string, XElement> ById, Dictionary<string, XElement> ByName) lookup,
            BlogMLPost post)
        {
            if (post.Id is not null && lookup.ById.TryGetValue(post.Id, out XElement? byId))
            {
                return byId;
            }

            return post.Name?.Content is { } name && lookup.ByName.TryGetValue(name, out XElement? byName) ? byName : null;
        }

        private IContent[] GetExistingPosts(IContent archiveNode)
        {
            IEnumerable<IContent> allPostNodes = contentService.EnumeratePagedChildren(
                archiveNode.Id,
                0,
                int.MaxValue,
                out _,
                sqlContext.Query<IContent>().Where(x => x.ParentId == archiveNode.Id && x.Trashed == false));

            return allPostNodes as IContent[] ?? [.. allPostNodes];
        }

        internal static IContent? FindExistingPost(
            Dictionary<string, IContent> postsByImportId,
            Dictionary<string, IContent> postsBySlug,
            BlogMLPost post,
            IShortStringHelper shortStringHelper)
        {
            if (!string.IsNullOrWhiteSpace(post.Id))
            {
                return postsByImportId.GetValueOrDefault(post.Id);
            }

            if (post.Name is null && string.IsNullOrWhiteSpace(post.Url?.OriginalString))
            {
                return null;
            }

            string slug = ExtractSlugFromPost(post).ToUrlSegment(shortStringHelper);
            return string.IsNullOrEmpty(slug) ? null : postsBySlug.GetValueOrDefault(slug);
        }

        private async Task PopulatePostContentAsync(
            IContentBase postNode,
            IContentType postType,
            BlogMLPost post,
            Dictionary<string, int> takenSlugs,
            string? urlNameCulture,
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

            SetPostSlug(postNode, post, takenSlugs, urlNameCulture);
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

        private void SetPostSlug(IContentBase postNode, BlogMLPost post, Dictionary<string, int> takenSlugs, string? culture)
        {
            if (post.Url is null || string.IsNullOrWhiteSpace(post.Url.OriginalString))
            {
                return;
            }

            string slug = EnsureUniqueSlug(ExtractSlugFromPost(post), takenSlugs, shortStringHelper);
            postNode.SetValue(Constants.Conventions.Content.UrlName, slug, culture);
        }

        // Counts, not a set: two existing siblings can already share a segment, and releasing one must not free the other.
        internal static Dictionary<string, int> CountSlugs(IEnumerable<IContentBase> nodes, IShortStringHelper shortStringHelper, string? culture)
        {
            var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (IContentBase node in nodes)
            {
                TakeSlug(taken, SlugSegment(node, shortStringHelper, culture));
            }

            return taken;
        }

        internal static void TakeSlug(Dictionary<string, int> taken, string? segment)
        {
            if (!string.IsNullOrEmpty(segment))
            {
                taken[segment] = taken.GetValueOrDefault(segment) + 1;
            }
        }

        internal static void ReleaseSlug(Dictionary<string, int> taken, string? segment)
        {
            if (string.IsNullOrEmpty(segment) || !taken.TryGetValue(segment, out int count))
            {
                return;
            }

            if (count > 1)
            {
                taken[segment] = count - 1;
            }
            else
            {
                _ = taken.Remove(segment);
            }
        }

        internal static string? SlugSegment(IContentBase node, IShortStringHelper shortStringHelper, string? culture)
        {
            string? source = node.GetValue<string>(Constants.Conventions.Content.UrlName, culture)?.Trim();
            if (string.IsNullOrWhiteSpace(source))
            {
                source = node.Name;
            }

            return string.IsNullOrWhiteSpace(source) ? null : source.ToUrlSegment(shortStringHelper);
        }

        // Mirrors Umbraco's DocumentRepository.EnsureUniqueUrlSegment, which only dedupes node names and never sees umbracoUrlName.
        internal static string EnsureUniqueSlug(string slug, Dictionary<string, int> taken, IShortStringHelper shortStringHelper)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return slug;
            }

            string proposed = slug.ToUrlSegment(shortStringHelper);
            if (string.IsNullOrEmpty(proposed))
            {
                return slug;
            }

            if (!taken.ContainsKey(proposed))
            {
                return slug;
            }

            for (var i = 1; ; i++)
            {
                var candidate = $"{slug} ({i})";
                string candidateSegment = candidate.ToUrlSegment(shortStringHelper);
                if (string.IsNullOrEmpty(candidateSegment) || !taken.ContainsKey(candidateSegment))
                {
                    return candidate;
                }
            }
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
            XElement? xmlPost,
            BlogMLCategory[] categories,
            Dictionary<string, string> authorIdsToName)
        {
            if (post.Authors.Count > 0)
            {
                if (authorIdsToName.TryGetValue(post.Authors[0], out string? name))
                {
                    await postNode
                        .SetInvariantOrDefaultCultureValueAsync("author", name, postType, languageService, logger);
                }
            }

            await ImportTagsAsync(xmlPost, postNode, postType);
            await ImportCategoriesAsync(postNode, post, categories, postType);
        }

        private void SaveAndPublishPost(IContent postNode, int userId, bool publishAll)
        {
            OperationResult saveResult = contentService.Save(postNode, userId);
            saveResult.EnsureSuccess(logger, $"save post {postNode.Id}");

            if (publishAll)
            {
                PublishResult publishResult = contentService.Publish(postNode, ["*"], userId);
                publishResult.EnsureSuccess(logger, $"publish post {postNode.Id}");
            }
        }
    }

    internal sealed record BlogMlImportFileSummary(int PostCount, int ExternalImageCount, string[] ExternalHosts);
}
