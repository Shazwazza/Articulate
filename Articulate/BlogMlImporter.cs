using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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

        public void Import(string fileName, int blogRootNode)
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

            using (var xmlReader = XmlReader.Create(File.OpenRead(fileName)))
            {
                var xpathReader = new XPathDocument(xmlReader);
                var document = new BlogMLDocument();
                document.Load(xpathReader);

                //var result = BlogMLSerializer.Deserialize(s);
                //ImportAuthors(root, result.Authors);
                //ImportCategories(root, result.Categories);
                ImportPosts(xpathReader, root, document.Posts, document.Authors.ToArray());
            }
        }

        private string ImportAuthor(IContent rootNode, BlogMLAuthor author)
        {
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
                //create teh authors node
                authorsNode = _applicationContext.Services.ContentService.CreateContent(
                    "Authors", rootNode, "ArticulateAuthors");
                _applicationContext.Services.ContentService.Save(authorsNode);
            }

            var allAuthorNodes = _applicationContext.Services.ContentService.GetContentOfContentType(authorType.Id).ToArray();

            //first check by email
            var found = _applicationContext.Services.UserService.GetByEmail(author.EmailAddress);
            if (found != null)
            {
                var authorNode = allAuthorNodes.FirstOrDefault(x => x.Name.InvariantEquals(found.Name));
                //check if an author node exists for this user
                if (authorNode == null)
                {
                    //create an author with the same name as the user - we'll need to wire up that 
                    // name to posts later on
                    authorNode = _applicationContext.Services.ContentService.CreateContent(
                        found.Name, authorsNode, "ArticulateAuthor");
                    _applicationContext.Services.ContentService.Save(authorNode);
                }
                return authorNode.Name;
            }
            else
            {
                var authorNode = allAuthorNodes.First(x => x.Name.InvariantEquals(author.Title.Content));
                //no user found with this email, so we'll check if it already exists by title
                if (authorNode == null)
                {
                    //create a new author node with this title
                    authorNode = _applicationContext.Services.ContentService.CreateContent(
                        author.Title.Content, authorsNode, "ArticulateAuthor");
                    _applicationContext.Services.ContentService.Save(authorNode);                    
                }
                return authorNode.Name;
            }
        }

        private void ImportPosts(XPathDocument xpath, IContent rootNode, IEnumerable<BlogMLPost> posts, BlogMLAuthor[] authors)
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

            foreach (var post in posts)
            {
                var postNode = _applicationContext.Services.ContentService.CreateContent(
                    post.Title.Content, archiveNode, "ArticulateRichText");

                postNode.SetValue("richText", post.Content.Content);
                
                //we're only going to use the last url segment
                var slug = post.Url.OriginalString.Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries);
                postNode.SetValue("umbracoUrlName", slug[slug.Length - 1].Split(new[] {'?'}, StringSplitOptions.RemoveEmptyEntries)[0]);

                if (post.Authors.Count > 0)
                {
                    var author = authors.FirstOrDefault(x => x.Id.InvariantEquals(post.Authors[0]));
                    //var name = ImportAuthor(rootNode, author);
                    //postNode.SetValue("author", name);
                }              
  
                //since this blobml serializer doesn't support tags (can't find one that does) we need to manually take care of that

            }
        }


        //private void ImportCategories(IContent rootNode, IEnumerable<BlogMLCategory> categories)
        //{
        //    var currentCategories = _applicationContext.Services.TagService.GetAllContentTags("ArticulateCategories").ToArray();
        //    foreach (var category in categories)
        //    {
        //        if (!currentCategories.Any(x => x.Text.InvariantEquals(category.Title)))
        //        {
                    
        //        }
        //    }
        //}
    }
}
