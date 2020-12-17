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
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;
using File = System.IO.File;
using Task = System.Threading.Tasks.Task;

namespace Articulate.ImportExport
{
    public class BlogMlImporter
    {
        private readonly ArticulateTempFileSystem _fileSystem;
        private readonly DisqusXmlExporter _disqusXmlExporter;
        private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IUserService _userService;
        private readonly ILogger _logger;
        private readonly IDataTypeService _dataTypeService;
        private readonly ISqlContext _sqlContext;
        private readonly IScopeProvider _scopeProvider;
        private readonly ILocalizationService _localizationService;

        public BlogMlImporter(
            ArticulateTempFileSystem fileSystem,
            DisqusXmlExporter disqusXmlExporter,
            IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
            IContentService contentService,
            IContentTypeService contentTypeService,
            IUserService userService,
            ILogger logger,
            IDataTypeService dataTypeService,
            ISqlContext sqlContext,
            IScopeProvider scopeProvider,
            ILocalizationService localizationService)
        {
            _fileSystem = fileSystem;
            _disqusXmlExporter = disqusXmlExporter;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _userService = userService;
            _logger = logger;
            _dataTypeService = dataTypeService;
            _sqlContext = sqlContext;
            _scopeProvider = scopeProvider;
            _localizationService = localizationService;
        }

        public int GetPostCount(string fileName)
        {
            var doc = GetDocument(fileName);
            return doc.Posts.Count();
        }

        /// <summary>
        /// Imports the blogml file to articulate
        /// </summary>
        /// <returns>Returns true if any errors occur</returns>
        // TODO: That is pretty silly, why return true for errors?! that's backwards, but need to maintain compat.
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
                    if (!File.Exists(fileName))
                    {
                        throw new FileNotFoundException("File not found: " + fileName);
                    }

                    var root = _contentService.GetById(blogRootNode);
                    if (root == null)
                    {
                        throw new InvalidOperationException("No node found with id " + blogRootNode);
                    }
                    if (!root.ContentType.Alias.InvariantEquals("Articulate"))
                    {
                        throw new InvalidOperationException("The node with id " + blogRootNode + " is not an Articulate root node");
                    }

                    using (var stream = File.OpenRead(fileName))
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
                                _fileSystem.AddFile("DisqusXmlExport.xml", memStream, true);
                            }
                        }
                    }

                    // commit
                    scope.Complete();
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.Error<BlogMlImporter>(ex, "Importing failed with errors");
                    return true;
                }
            }
        }

        private BlogMLDocument GetDocument(string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("File not found: " + fileName);
            }

            using (var stream = File.OpenRead(fileName))
            {
                var document = new BlogMLDocument();
                document.Load(stream);
                return document;
            }
        }

        private IDictionary<string, string> ImportAuthors(int userId, IContent rootNode, IEnumerable<BlogMLAuthor> authors)
        {
            var result = new Dictionary<string, string>();

            var authorType = _contentTypeService.Get("ArticulateAuthor");
            if (authorType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateAuthor doc type could not be found");
            }
            
            var authorsType = _contentTypeService.Get(ArticulateConstants.ArticulateAuthorsContentTypeAlias);
            if (authorsType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateAuthors doc type could not be found");
            }

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

            var postType = _contentTypeService.Get("ArticulateRichText");
            if (postType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateRichText doc type could not be found");
            }

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
                if (!overwrite && postNode != null) continue;

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
                        excerpt = Encoding.UTF8.GetString(Convert.FromBase64String(post.Excerpt.Content));

                    postNode.SetInvariantOrDefaultCultureValue("excerpt", excerpt, postType, _localizationService);
                }

                postNode.SetInvariantOrDefaultCultureValue("importId", post.Id, postType, _localizationService);

                var content = post.Content.Content;

                if (post.Content.ContentType == BlogMLContentType.Base64)
                    content = Encoding.UTF8.GetString(Convert.FromBase64String(post.Content.Content));

                if (!regexMatch.IsNullOrWhiteSpace() && !regexReplace.IsNullOrWhiteSpace())
                {
                    //run the replacement
                    content = Regex.Replace(content, regexMatch, regexReplace,
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                }

                postNode.SetInvariantOrDefaultCultureValue("richText", content, postType, _localizationService);

                postNode.SetInvariantOrDefaultCultureValue("enableComments", true, postType, _localizationService);

                if (post.Url != null && !string.IsNullOrWhiteSpace(post.Url.OriginalString))
                {
                    var slug = string.Empty;
                    //we take the post-name BlogML element as slug for the post
                    if (post.Name != null)
                        slug = post.Name.Content;
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
            if (attachment == null) return;

            var imageSaved = false;

            if (!attachment.Content.IsNullOrWhiteSpace())
            {
                //the image is base64
                var bytes = Convert.FromBase64String(attachment.Content);
                using (var stream = new MemoryStream(bytes))
                {
                    postNode.SetInvariantOrDefaultCultureValue(_contentTypeBaseServiceProvider, "postImage", attachment.Url.OriginalString, stream, postType, _localizationService);
                    imageSaved = true;
                }
            }
            else if (attachment.ExternalUri != null && attachment.ExternalUri.IsAbsoluteUri)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        using (var stream = await client.GetStreamAsync(attachment.ExternalUri))
                        {
                            postNode.SetInvariantOrDefaultCultureValue(_contentTypeBaseServiceProvider, "postImage", Path.GetFileName(attachment.ExternalUri.AbsolutePath), stream, postType, _localizationService);
                            imageSaved = true;
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.Error<BlogMlImporter>(exception, "Exception retrieving {AttachmentUrl}; post {PostId}", attachment.Url, post.Id);
                }
            }

            if (imageSaved)
            {
                //this is a work around for the SetValue method to save a file, since it doesn't currently take into account the image cropper
                //which we are using so we need to fix that.
                
                var propType = postNode.Properties["postImage"].PropertyType;
                var cropperValue = CreateImageCropperValue(propType, postNode.GetValue("postImage"), _dataTypeService);
                postNode.SetInvariantOrDefaultCultureValue("postImage", cropperValue, postType, _localizationService);
            }
            
        }

        //borrowed from CMS core until SetValue is fixed with a stream
        private string CreateImageCropperValue(PropertyType propertyType, object value, IDataTypeService dataTypeService)
        {
            if (value == null || string.IsNullOrEmpty(value.ToString()))
                return null;

            // if we don't have a json structure, we will get it from the property type
            var val = value.ToString();
            if (val.DetectIsJson())
                return val;

            var configuration = dataTypeService.GetDataType(propertyType.DataTypeId).ConfigurationAs<ImageCropperConfiguration>();
            var crops = configuration?.Crops ?? Array.Empty<ImageCropperConfiguration.Crop>();

            return JsonConvert.SerializeObject(new
            {
                src = val,
                crops = crops
            });
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

            postNode.AssignInvariantOrDefaultCultureTags("categories", postCats, postType, _localizationService);
        }

        private void ImportTags(XDocument xdoc, IContent postNode, BlogMLPost post, IContentType postType)
        {
            //since this blobml serializer doesn't support tags (can't find one that does) we need to manually take care of that
            var xmlPost = xdoc.Descendants(XName.Get("post", xdoc.Root.Name.NamespaceName))
                .SingleOrDefault(x => ((string)x.Attribute("id")) == post.Id);

            if (xmlPost == null) {
                xmlPost = xdoc.Descendants(XName.Get("post", xdoc.Root.Name.NamespaceName))
                                .SingleOrDefault(x => x.Descendants(XName.Get("post-name", xdoc.Root.Name.NamespaceName))
                                .SingleOrDefault(s => s.Value==post.Name.Content)!=null
                                );
            };

            if (xmlPost == null) return;

            var tags = xmlPost.Descendants(XName.Get("tag", xdoc.Root.Name.NamespaceName)).Select(x => (string)x.Attribute("ref")).ToArray();

            postNode.AssignInvariantOrDefaultCultureTags("tags", tags, postType, _localizationService);
        }
    }
}
