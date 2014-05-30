using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Argotic.Common;
using Argotic.Syndication.Specialized;
using Newtonsoft.Json.Serialization;
using Umbraco.Core;
using Umbraco.Core.Models;
using File = System.IO.File;

namespace Articulate
{
    public class BlogMlImporter
    {
        private readonly ApplicationContext _applicationContext;

        public BlogMlImporter(ApplicationContext applicationContext)
        {
            _applicationContext = applicationContext;

        }

        public void Import(string fileName, int blogRootNode, bool overwrite)
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

                var authorIdsToName = ImportAuthors(root, document.Authors);

                ImportPosts(xdoc, root, document.Posts, document.Authors.ToArray(), document.Categories.ToArray(), authorIdsToName, overwrite);
            }
        }

        private IDictionary<string, string> ImportAuthors(IContent rootNode, IEnumerable<BlogMLAuthor> authors)
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
                _applicationContext.Services.ContentService.Save(authorsNode);
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
                        _applicationContext.Services.ContentService.Save(authorNode);
                    }

                    result.Add(author.Id, authorNode.Name);

                }
                else
                {
                    //no user existsw with this email, so check if a node exists with the current author's title
                    var authorNode = allAuthorNodes.First(x => x.Name.InvariantEquals(author.Title.Content));
                    
                    //nope, not found so create one
                    if (authorNode == null)
                    {
                        //create a new author node with this title
                        authorNode = _applicationContext.Services.ContentService.CreateContent(
                            author.Title.Content, authorsNode, "ArticulateAuthor");
                        _applicationContext.Services.ContentService.Save(authorNode);
                    }

                    result.Add(author.Id, authorNode.Name);
                }
            }

            return result;

        }

        private void ImportPosts(XDocument xdoc, IContent rootNode, IEnumerable<BlogMLPost> posts, BlogMLAuthor[] authors, BlogMLCategory[] categories, IDictionary<string, string> authorIdsToName, bool overwrite)
        {
            
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
                
                postNode.CreateDate = post.CreatedOn;
                postNode.UpdateDate = post.LastModifiedOn;

                //TODO: Do Excerpt

                postNode.SetValue("importId", post.Id);
                postNode.SetValue("richText", post.Content.Content);

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

                _applicationContext.Services.ContentService.Save(postNode);
            }
        }

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
