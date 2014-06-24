using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Argotic.Common;
using Argotic.Syndication.Specialized;
using Newtonsoft.Json.Serialization;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using File = System.IO.File;
using Task = System.Threading.Tasks.Task;

namespace Articulate
{
    public class BlogMlImporter
    {
        public BlogMlImporter()
        {
            HasErrors = false;
        }

        public bool HasErrors { get; private set; }

        private readonly ApplicationContext _applicationContext;

        public BlogMlImporter(ApplicationContext applicationContext)
        {
            _applicationContext = applicationContext;

        }

        public int GetPostCount(string fileName)
        {
            var doc = GetDocument(fileName);
            return doc.Posts.Count();
        }

        public async Task Import(
            int userId, 
            string fileName, 
            int blogRootNode, 
            bool overwrite, 
            string regexMatch, 
            string regexReplace, 
            bool publishAll,
            bool exportDisqusXml = false)
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    throw new FileNotFoundException("File not found: " + fileName);
                }

                var root = _applicationContext.Services.ContentService.GetById(blogRootNode);
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

                    var imported = await ImportPosts(userId, xdoc, root, document.Posts, document.Authors.ToArray(), document.Categories.ToArray(), authorIdsToName, overwrite, regexMatch, regexReplace, publishAll);

                    if (exportDisqusXml)
                    {
                        var exporter = new DisqusXmlExporter();
                        var xDoc = exporter.Export(imported, document);

                        using (var memStream = new MemoryStream())
                        {
                            xDoc.Save(memStream);
                            
                            var mediaFs = FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>();

                            mediaFs.AddFile("Articulate/DisqusXmlExport.xml", memStream, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HasErrors = true;
                LogHelper.Error<BlogMlImporter>("Importing failed with errors", ex);
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

            var authorType = _applicationContext.Services.ContentTypeService.GetContentType("ArticulateAuthor");
            if (authorType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateAuthor doc type could not be found");
            }

            //TODO: Check for existence of ArticulateAuthors

            var children = rootNode.Children().ToArray();
            var authorsNode = children.FirstOrDefault(x => x.ContentType.Alias.InvariantEquals("ArticulateAuthors"));
            if (authorsNode == null)
            {
                //create the authors node
                authorsNode = _applicationContext.Services.ContentService.CreateContent(
                    "Authors", rootNode, "ArticulateAuthors");
                _applicationContext.Services.ContentService.SaveAndPublishWithStatus(authorsNode, userId);
            }

            var allAuthorNodes = _applicationContext.Services.ContentService.GetContentOfContentType(authorType.Id).ToArray();

            foreach (var author in authors)
            {
                //first check if a user exists by email
                var found = _applicationContext.Services.UserService.GetByEmail(author.EmailAddress);
                if (found != null)
                {
                    //check if an author node exists for this user
                    var authorNode = allAuthorNodes.FirstOrDefault(x => x.Name.InvariantEquals(found.Name));
                    
                    //nope not found so create a node for this user name
                    if (authorNode == null)
                    {
                        //create an author with the same name as the user - we'll need to wire up that 
                        // name to posts later on
                        authorNode = _applicationContext.Services.ContentService.CreateContent(
                            found.Name, authorsNode, "ArticulateAuthor");
                        _applicationContext.Services.ContentService.SaveAndPublishWithStatus(authorNode, userId);
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
                        authorNode = _applicationContext.Services.ContentService.CreateContent(
                            author.Title.Content, authorsNode, "ArticulateAuthor");
                        _applicationContext.Services.ContentService.SaveAndPublishWithStatus(authorNode, userId);
                    }

                    result.Add(author.Id, authorNode.Name);
                }
            }

            return result;

        }

        private async Task<IEnumerable<IContent>>  ImportPosts(int userId, XDocument xdoc, IContent rootNode, IEnumerable<BlogMLPost> posts, BlogMLAuthor[] authors, BlogMLCategory[] categories, IDictionary<string, string> authorIdsToName, bool overwrite, string regexMatch, string regexReplace, bool publishAll)
        {

            var result = new List<IContent>();

            var postType = _applicationContext.Services.ContentTypeService.GetContentType("ArticulateRichText");
            if (postType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateRichText doc type could not be found");
            }

            //TODO: Check for existence of ArticulateArchive

            var children = rootNode.Children().ToArray();

            var archiveNode = children.FirstOrDefault(x => x.ContentType.Alias.InvariantEquals("ArticulateArchive"));

            if (archiveNode == null)
            {
                //create teh authors node
                archiveNode = _applicationContext.Services.ContentService.CreateContent(
                    "Articles", rootNode, "ArticulateArchive");
                _applicationContext.Services.ContentService.Save(archiveNode);
            }

            var allPostNodes = archiveNode.Children().ToArray();

            foreach (var post in posts)
            {
                //check if one exists
                var postNode = allPostNodes.FirstOrDefault(x => x.GetValue<string>("importId") == post.Id);
                
                //it exists and we don't wanna overwrite, skip it
                if (!overwrite && postNode != null) continue;

                //create it if it doesn't exist
                if (postNode == null)
                {
                    postNode = _applicationContext.Services.ContentService.CreateContent(
                        post.Title.Content, archiveNode, "ArticulateRichText");        
                }

                postNode.SetValue("publishedDate", post.CreatedOn);

                if (post.Excerpt != null && post.Excerpt.Content.IsNullOrWhiteSpace() == false)
                {
                    postNode.SetValue("excerpt", post.Excerpt.Content);
                }

                postNode.SetValue("importId", post.Id);

                var content = post.Content.Content;
                if (!regexMatch.IsNullOrWhiteSpace() && !regexReplace.IsNullOrWhiteSpace())
                {
                    //run the replacement
                    content = Regex.Replace(content, regexMatch, regexReplace, 
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);    
                }

                postNode.SetValue("richText", content);
                postNode.SetValue("enableComments", true);

                //we're only going to use the last url segment
                var slug = post.Url.OriginalString.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                postNode.SetValue("umbracoUrlName", slug[slug.Length - 1].Split(new[] { '?' }, StringSplitOptions.RemoveEmptyEntries)[0]);

                if (post.Authors.Count > 0)
                {
                    var author = authors.FirstOrDefault(x => x.Id.InvariantEquals(post.Authors[0]));
                    
                    if (author != null)
                    {
                        var name = authorIdsToName[author.Id];
                        postNode.SetValue("author", name);   
                    }                    
                }

                ImportTags(xdoc, postNode, post);
                ImportCategories(postNode, post, categories);
                
                if (publishAll)
                {
                    _applicationContext.Services.ContentService.SaveAndPublishWithStatus(postNode, userId);
                }
                else
                {
                    _applicationContext.Services.ContentService.Save(postNode, userId);    
                }

                //if (!publicKey.IsNullOrWhiteSpace())
                //{
                //    await ImportComments(userId, postNode, post, publicKey);
                //}

                result.Add(postNode);
            }

            return result;
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
        //            postNode.SetValue("disqusCommentsImported", 1);
        //            //just save it, we don't need to publish it (if publish = true then its already published), we just need
        //            // this for reference.
        //            _applicationContext.Services.ContentService.Save(postNode, userId);    
        //        }
        //    }
        //}

        private void ImportCategories(IContent postNode, BlogMLPost post, IEnumerable<BlogMLCategory> allCategories)
        {
            var postCats = allCategories.Where(x => post.Categories.Contains(x.Id))
                .Select(x => x.Title.Content)
                .ToArray();

            postNode.SetTags("categories", postCats, true, "ArticulateCategories");
        }

        private void ImportTags(XDocument xdoc, IContent postNode, BlogMLPost post)
        {
            //since this blobml serializer doesn't support tags (can't find one that does) we need to manually take care of that
            var xmlPost = xdoc.Descendants(XName.Get("post", xdoc.Root.Name.NamespaceName))
                .SingleOrDefault(x => ((string)x.Attribute("id")) == post.Id);

            var tags = xmlPost.Descendants(XName.Get("tag", xdoc.Root.Name.NamespaceName)).Select(x => (string)x.Attribute("ref")).ToArray();
            postNode.SetTags("tags", tags, true, "ArticulateTags");
        }

    }
}
