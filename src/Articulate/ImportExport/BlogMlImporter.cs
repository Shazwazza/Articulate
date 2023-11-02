using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Argotic.Syndication.Specialized;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;
using File = System.IO.File;
using Task = System.Threading.Tasks.Task;

namespace Articulate.ImportExport
{
    public class BlogMlImporter
    {
        private readonly DisqusXmlExporter _disqusXmlExporter;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
        private readonly IContentService _contentService;
        private readonly IMediaService _mediaService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IUserService _userService;
        private readonly ILogger<BlogMlImporter> _logger;
        private readonly IDataTypeService _dataTypeService;
        private readonly ISqlContext _sqlContext;
        private readonly IScopeProvider _scopeProvider;
        private readonly ILocalizationService _localizationService;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly MediaFileManager _mediaFileManager;
        private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
        private readonly PropertyEditorCollection _dataEditors;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ArticulateTempFileSystem _articulateTempFileSystem;
        private readonly Lazy<IMedia> _articulateRootMediaFolder;

        public BlogMlImporter(
            DisqusXmlExporter disqusXmlExporter,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
            IContentService contentService,
            IMediaService mediaService,
            IContentTypeService contentTypeService,
            IUserService userService,
            ILogger<BlogMlImporter> logger,
            IDataTypeService dataTypeService,
            ISqlContext sqlContext,
            IScopeProvider scopeProvider,
            ILocalizationService localizationService,
            IShortStringHelper shortStringHelper,
            MediaFileManager mediaFileManager,
            MediaUrlGeneratorCollection mediaUrlGenerators,
            PropertyEditorCollection dataEditors,
            IJsonSerializer jsonSerializer,
            ArticulateTempFileSystem articulateTempFileSystem)
        {
            _disqusXmlExporter = disqusXmlExporter;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
            _contentService = contentService;
            _mediaService = mediaService;
            _contentTypeService = contentTypeService;
            _userService = userService;
            _logger = logger;
            _dataTypeService = dataTypeService;
            _sqlContext = sqlContext;
            _scopeProvider = scopeProvider;
            _localizationService = localizationService;
            _shortStringHelper = shortStringHelper;
            _mediaFileManager = mediaFileManager;
            _mediaUrlGenerators = mediaUrlGenerators;
            _dataEditors = dataEditors;
            _jsonSerializer = jsonSerializer;
            _articulateTempFileSystem = articulateTempFileSystem;
            _articulateRootMediaFolder = new Lazy<IMedia>(() =>
            {
                var root = _mediaService.GetRootMedia().FirstOrDefault(x => x.Name == "Articulate" && x.ContentType.Alias.InvariantEquals("folder"));
                return root ??= _mediaService.CreateMediaWithIdentity("Articulate", Constants.System.Root, "folder");
            });
        }

        public int GetPostCount(string fileName)
        {
            var doc = GetDocument(fileName);
            return doc.Posts.Count();
        }

        /// <summary>
        /// Imports the blogml file to articulate
        /// </summary>
        /// <returns>Returns true if successful</returns>
        public async Task<bool> Import(
            int userId,
            string fileName,
            int blogRootNode,
            bool overwrite,
            string regexMatch,
            string regexReplace,
            bool publishAll,
            bool exportDisqusXml = false,
            bool importFirstImage = false)
        {
            // wrap entire operation in scope
            using (var scope = _scopeProvider.CreateScope())
            {
                try
                {
                    if (!_articulateTempFileSystem.FileExists(fileName))
                    {
                        throw new FileNotFoundException("File not found: " + fileName);
                    }

                    var root = _contentService.GetById(blogRootNode)
                        ?? throw new InvalidOperationException("No node found with id " + blogRootNode);

                    if (!root.ContentType.Alias.InvariantEquals("Articulate"))
                    {
                        throw new InvalidOperationException("The node with id " + blogRootNode + " is not an Articulate root node");
                    }

                    using (var stream = _articulateTempFileSystem.OpenFile(fileName))
                    {
                        var document = new BlogMLDocument();
                        document.Load(stream);

                        stream.Position = 0;
                        var xdoc = XDocument.Load(stream);

                        var authorIdsToName = ImportAuthors(userId, root, document.Authors);

                        var imported = await ImportPosts(userId, xdoc, root, document.Posts, document.Authors.ToArray(), document.Categories.ToArray(), authorIdsToName, overwrite, regexMatch, regexReplace, publishAll, importFirstImage);

                        if (exportDisqusXml)
                        {
                            var xDoc = _disqusXmlExporter.Export(imported, document);

                            using (var memStream = new MemoryStream())
                            {
                                xDoc.Save(memStream);
                                _articulateTempFileSystem.AddFile("DisqusXmlExport.xml", memStream, true);
                            }
                        }
                    }

                    // commit
                    scope.Complete();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Importing failed with errors");
                    return false;
                }
            }
        }

        private BlogMLDocument GetDocument(string fileName)
        {
            if (!_articulateTempFileSystem.FileExists(fileName))
            {
                throw new FileNotFoundException("File not found: " + fileName);
            }

            using (var stream = _articulateTempFileSystem.OpenFile(fileName))
            {
                var document = new BlogMLDocument();
                document.Load(stream);
                return document;
            }
        }

        private IDictionary<string, string> ImportAuthors(int userId, IContent rootNode, IEnumerable<BlogMLAuthor> authors)
        {
            var result = new Dictionary<string, string>();

            var authorType = _contentTypeService.Get("ArticulateAuthor")
                ?? throw new InvalidOperationException("Articulate is not installed properly, the ArticulateAuthor doc type could not be found");

            var authorsType = _contentTypeService.Get(ArticulateConstants.ArticulateAuthorsContentTypeAlias)
                ?? throw new InvalidOperationException("Articulate is not installed properly, the ArticulateAuthors doc type could not be found");

            // get the authors container node for this articulate root
            var allAuthorsNodes = _contentService.GetPagedOfType(
                authorsType.Id,
                0,
                int.MaxValue,
                out long totalAuthorsNodes,
                _sqlContext.Query<IContent>().Where(x => x.ParentId == rootNode.Id && x.Trashed == false));

            var authorsNode = allAuthorsNodes.FirstOrDefault();
            if (authorsNode == null)
            {
                //create the authors node
                authorsNode = _contentService.CreateWithInvariantOrDefaultCultureName(ArticulateConstants.AuthorsDefaultName, rootNode, authorsType, _localizationService);

                _contentService.SaveAndPublish(authorsNode, userId: userId);
            }

            // get the authors nodes for this authors container
            var allAuthorNodes = _contentService.GetPagedOfType(
                authorType.Id,
                0,
                int.MaxValue,
                out long totalAuthorNodes,
                _sqlContext.Query<IContent>().Where(x => x.ParentId == authorsNode.Id && x.Trashed == false));

            foreach (var author in authors)
            {
                //first check if a user exists by email
                var found = _userService.GetByEmail(author.EmailAddress);
                if (found != null)
                {
                    //check if an author node exists for this user
                    var authorNode = allAuthorNodes.FirstOrDefault(x => x.Name.InvariantEquals(found.Name));

                    //nope not found so create a node for this user name
                    if (authorNode == null)
                    {
                        //create an author with the same name as the user - we'll need to wire up that
                        // name to posts later on
                        authorNode = _contentService.CreateWithInvariantOrDefaultCultureName(found.Name, authorsNode, authorType, _localizationService);

                        _contentService.SaveAndPublish(authorNode, userId: userId);
                    }

                    result.Add(author.Id, authorNode.Name);
                }
                else
                {
                    //no user existsw with this email, so check if a node exists with the current author's title
                    var authorNode = allAuthorNodes.FirstOrDefault(x => x.Name.InvariantEquals(author.Title.Content));

                    //nope, not found so create one
                    if (authorNode == null)
                    {
                        //create a new author node with this title
                        authorNode = _contentService.CreateWithInvariantOrDefaultCultureName(author.Title.Content, authorsNode, authorType, _localizationService);

                        _contentService.SaveAndPublish(authorNode, userId: userId);
                    }

                    result.Add(author.Id, authorNode.Name);
                }
            }

            return result;
        }

        private async Task<IEnumerable<IContent>> ImportPosts(int userId, XDocument xdoc, IContent rootNode, IEnumerable<BlogMLPost> posts, BlogMLAuthor[] authors, BlogMLCategory[] categories, IDictionary<string, string> authorIdsToName, bool overwrite, string regexMatch, string regexReplace, bool publishAll, bool importFirstImage = false)
        {
            var result = new List<IContent>();

            var postType = _contentTypeService.Get("ArticulateRichText")
                ?? throw new InvalidOperationException("Articulate is not installed properly, the ArticulateRichText doc type could not be found");

            var archiveDocType = _contentTypeService.Get(ArticulateConstants.ArticulateArchiveContentTypeAlias);

            // get the archive container node for this articulate root
            var archive = _contentService.GetPagedOfType(
                archiveDocType.Id,
                0,
                int.MaxValue,
                out long totalArchives,
                _sqlContext.Query<IContent>().Where(x => x.ParentId == rootNode.Id && x.Trashed == false));

            var archiveNode = archive.FirstOrDefault();

            if (archiveNode == null)
            {
                //create the authors node
                archiveNode = _contentService.CreateWithInvariantOrDefaultCultureName(ArticulateConstants.ArticlesDefaultName, rootNode, archiveDocType, _localizationService);

                _contentService.Save(archiveNode);
            }

            // get the posts for this archive container
            var allPostNodes = _contentService.GetPagedChildren(
                archiveNode.Id,
                0,
                int.MaxValue,
                out long totalPostNodes,
                _sqlContext.Query<IContent>().Where(x => x.ParentId == archiveNode.Id && x.Trashed == false));

            foreach (var post in posts)
            {
                //check if one exists

                IContent postNode;

                //Use post.id if it's there
                if (!string.IsNullOrWhiteSpace(post.Id))
                {
                    postNode = allPostNodes.FirstOrDefault(x => x.GetValue<string>("importId") == post.Id);
                }
                else
                {
                    //Use the "slug" (post name) if post.id is not there
                    postNode = allPostNodes
                        .FirstOrDefault(x => x.GetValue<string>("umbracoUrlName") != null
                                             && x.GetValue<string>("umbracoUrlName").InvariantStartsWith(post.Name.Content));
                }

                //it exists and we don't wanna overwrite, skip it
                if (!overwrite && postNode != null)
                {
                    continue;
                }

                //create it if it doesn't exist
                if (postNode == null)
                {
                    var title = WebUtility.HtmlDecode(post.Title.Content);
                    postNode = _contentService.CreateWithInvariantOrDefaultCultureName(title, archiveNode, postType, _localizationService);
                }

                var propType = postType.CompositionPropertyTypes.First(x => x.Alias == "publishedDate");

                postNode.SetInvariantOrDefaultCultureValue("publishedDate", post.CreatedOn, postType, _localizationService);

                if (post.Excerpt != null && post.Excerpt.Content.IsNullOrWhiteSpace() == false)
                {
                    var excerpt = post.Excerpt.Content;

                    if (post.Excerpt.ContentType == BlogMLContentType.Base64)
                    {
                        excerpt = Encoding.UTF8.GetString(Convert.FromBase64String(post.Excerpt.Content));
                    }

                    postNode.SetInvariantOrDefaultCultureValue("excerpt", excerpt, postType, _localizationService);
                }

                postNode.SetInvariantOrDefaultCultureValue("importId", post.Id, postType, _localizationService);

                var content = post.Content.Content;

                if (post.Content.ContentType == BlogMLContentType.Base64)
                {
                    content = Encoding.UTF8.GetString(Convert.FromBase64String(post.Content.Content));
                }

                if (!regexMatch.IsNullOrWhiteSpace() && !regexReplace.IsNullOrWhiteSpace())
                {
                    //run the replacement
                    content = Regex.Replace(content, regexMatch, regexReplace,
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                }

                // This apparently now needs to be saved as an HtmlString before hand,
                // see https://docs.umbraco.com/umbraco-cms/fundamentals/backoffice/property-editors/built-in-umbraco-property-editors/rich-text-editor#add-values-programmatically
                postNode.SetInvariantOrDefaultCultureValue("richText", new HtmlString(content), postType, _localizationService);

                postNode.SetInvariantOrDefaultCultureValue("enableComments", true, postType, _localizationService);

                if (post.Url != null && !string.IsNullOrWhiteSpace(post.Url.OriginalString))
                {
                    var slug = string.Empty;
                    //we take the post-name BlogML element as slug for the post
                    if (post.Name != null)
                    {
                        slug = post.Name.Content;
                    }
                    //If post-name is not available we take the URL and remove the extension
                    else
                    {
                        var slugArray = post.Url.OriginalString.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        var fileNameAndQuery = slugArray[slugArray.Length - 1];
                        var fileNameAndQueryArray = fileNameAndQuery.Split(new[] { '?' }, StringSplitOptions.RemoveEmptyEntries);
                        var fileName = fileNameAndQueryArray[fileNameAndQueryArray.Length - 1];
                        var fileNameArray = fileName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                        var ext = fileNameArray[fileNameArray.Length - 1];
                        slug = fileName.TrimEnd("." + ext);
                    }

                    postNode.SetInvariantOrDefaultCultureValue("umbracoUrlName", slug, postType, _localizationService);
                }

                if (post.Authors.Count > 0)
                {
                    var author = authors.FirstOrDefault(x => x.Id.InvariantEquals(post.Authors[0]));

                    if (author != null)
                    {
                        var name = authorIdsToName[author.Id];
                        postNode.SetInvariantOrDefaultCultureValue("author", name, postType, _localizationService);
                    }
                }

                ImportTags(xdoc, postNode, post, postType);
                ImportCategories(postNode, post, categories, postType);
                if (importFirstImage)
                {
                    await ImportFirstImageAsync(postNode, postType, post);
                }

                if (publishAll)
                {
                    _contentService.SaveAndPublish(postNode, userId: userId);
                }
                else
                {
                    _contentService.Save(postNode, userId);
                }

                //if (!publicKey.IsNullOrWhiteSpace())
                //{
                //    await ImportComments(userId, postNode, post, publicKey);
                //}

                result.Add(postNode);
            }

            return await Task.FromResult(result);
        }

        private async Task ImportFirstImageAsync(IContentBase postNode, IContentType postType, BlogMLPost post)
        {
            var imageMimeTypes = new List<string> { "image/jpeg", "image/gif", "image/png" };

            var attachment = post.Attachments.FirstOrDefault(p => imageMimeTypes.Contains(p.MimeType));
            if (attachment == null)
            {
                return;
            }

            Stream stream = null;
            if (!attachment.Content.IsNullOrWhiteSpace())
            {
                //the image is base64
                var bytes = Convert.FromBase64String(attachment.Content);
                stream = new MemoryStream(bytes);
            }
            else if (attachment.ExternalUri != null && attachment.ExternalUri.IsAbsoluteUri)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        stream = await client.GetStreamAsync(attachment.ExternalUri);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Exception retrieving {AttachmentUrl}; post {PostId}", attachment.Url, post.Id);
                }
            }

            if (stream != null)
            {
                using (stream)
                {
                    // create a media item
                    var media = _mediaService.CreateMedia(postNode.Name, _articulateRootMediaFolder.Value, Constants.Conventions.MediaTypes.Image);
                    media.SetValue(
                        _mediaFileManager,
                        _mediaUrlGenerators,
                        _shortStringHelper,
                        _contentTypeBaseServiceProvider,
                        Constants.Conventions.Media.File,
                        attachment.Url.OriginalString,
                        stream);

                    if (!_mediaService.Save(media))
                    {
                        throw new InvalidOperationException("Could not create new media item");
                    }

                    // Create an Udi of the media
                    var udi = Udi.Create(Constants.UdiEntityType.Media, media.Key);

                    postNode.SetInvariantOrDefaultCultureValue(
                        "postImage",
                        udi.ToString(),
                        postType,
                        _localizationService);
                }
            }
        }

        //private async Task ImportComments(int userId, IContent postNode, BlogMLPost post,
        //    string publicKey/*, string privateKey, string accessToken*/)
        //{
        //    var importer = new DisqusImporter(publicKey);

        //    foreach (var comment in post.Comments)
        //    {
        //        var result = await importer.Import(
        //            postNode.Id.ToString(CultureInfo.InvariantCulture),
        //            comment.Content.Content,
        //            comment.UserName,
        //            comment.UserEmailAddress,
        //            comment.UserUrl != null ? comment.UserUrl.ToString() : string.Empty,
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
        //}

        private void ImportCategories(IContent postNode, BlogMLPost post, IEnumerable<BlogMLCategory> allCategories, IContentType postType)
        {
            var postCats = allCategories.Where(x => post.Categories.Contains(x.Id))
                .Select(x => x.Title.Content)
                .ToArray();

            postNode.AssignInvariantOrDefaultCultureTags("categories", postCats, postType, _localizationService, _dataTypeService, _dataEditors, _jsonSerializer);
        }

        private void ImportTags(XDocument xdoc, IContent postNode, BlogMLPost post, IContentType postType)
        {
            //since this blobml serializer doesn't support tags (can't find one that does) we need to manually take care of that
            var xmlPost = xdoc.Descendants(XName.Get("post", xdoc.Root.Name.NamespaceName))
                .SingleOrDefault(x => ((string)x.Attribute("id")) == post.Id);

            xmlPost ??= xdoc.Descendants(XName.Get("post", xdoc.Root.Name.NamespaceName))
                                .SingleOrDefault(x => x.Descendants(XName.Get("post-name", xdoc.Root.Name.NamespaceName))
                                .SingleOrDefault(s => s.Value == post.Name.Content) != null
                                );;

            if (xmlPost == null)
            {
                return;
            }

            var tags = xmlPost.Descendants(XName.Get("tag", xdoc.Root.Name.NamespaceName)).Select(x => (string)x.Attribute("ref")).ToArray();

            postNode.AssignInvariantOrDefaultCultureTags("tags", tags, postType, _localizationService, _dataTypeService, _dataEditors, _jsonSerializer);
        }
    }
}
