using Argotic.Common;
using Argotic.Syndication.Specialized;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Articulate
{
    public class BlogMlExporter
    {
        private readonly IFileSystem _fileSystem;
        private readonly ApplicationContext _applicationContext;
        private readonly UmbracoContext _umbracoContext;

        public BlogMlExporter(UmbracoContext umbracoContext, IFileSystem fileSystem)
        {
            _umbracoContext = umbracoContext;
            _applicationContext = _umbracoContext.Application;
            _fileSystem = fileSystem;
        }

        public void Export(
            string fileName,
            int blogRootNode)
        {
            var root = _applicationContext.Services.ContentService.GetById(blogRootNode);
            if (root == null)
            {
                throw new InvalidOperationException("No node found with id " + blogRootNode);
            }
            if (!root.ContentType.Alias.InvariantEquals("Articulate"))
            {
                throw new InvalidOperationException("The node with id " + blogRootNode + " is not an Articulate root node");
            }

            var postType = _applicationContext.Services.ContentTypeService.GetContentType("ArticulateRichText");
            if (postType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateRichText doc type could not be found");
            }

            var children = root.Children().ToArray();

            var archiveNode = children.FirstOrDefault(x => x.ContentType.Alias.InvariantEquals("ArticulateArchive"));
            var authorsNode = children.FirstOrDefault(x => x.ContentType.Alias.InvariantEquals("ArticulateAuthors"));
            if (archiveNode == null)
            {
                throw new InvalidOperationException("No ArticulateArchive found under the blog root node");
            }
            if (authorsNode == null)
            {
                throw new InvalidOperationException("No ArticulateAuthors found under the blog root node");
            }
            var categoryDataType = _applicationContext.Services.DataTypeService.GetDataTypeDefinitionByName("Articulate Categories");
            if (categoryDataType == null)
            {
                throw new InvalidOperationException("No Articulate Categories data type found");
            }
            var categoryDtPreVals = _applicationContext.Services.DataTypeService.GetPreValuesCollectionByDataTypeId(categoryDataType.Id);
            if (categoryDtPreVals == null)
            {
                throw new InvalidOperationException("No pre values for Articulate Categories data type found");
            }
            var tagGroup = categoryDtPreVals.PreValuesAsDictionary["group"];

            //TODO: See: http://argotic.codeplex.com/wikipage?title=Generating%20portable%20web%20log%20content&referringTitle=Home

            var blogMlDoc = new BlogMLDocument
            {
                RootUrl = new Uri(_umbracoContext.UrlProvider.GetUrl(root.Id), UriKind.RelativeOrAbsolute),
                GeneratedOn = DateTime.Now,
                Title = new BlogMLTextConstruct(root.GetValue<string>("blogTitle")),
                Subtitle = new BlogMLTextConstruct(root.GetValue<string>("blogDescription"))
            };

            AddBlogAuthors(authorsNode, blogMlDoc);
            AddBlogCategories(blogMlDoc, tagGroup.Value);
            AddBlogPosts(archiveNode, blogMlDoc, tagGroup.Value);

            WriteFile(blogMlDoc);
        }

        private void WriteFile(BlogMLDocument blogMlDoc)
        {
            using (var stream = new MemoryStream())
            {
                blogMlDoc.Save(stream, new SyndicationResourceSaveSettings()
                {
                    CharacterEncoding = Encoding.UTF8
                });
                stream.Position = 0;
                _fileSystem.AddFile("BlogMlExport.xml", stream, true);
            }
        }

        private void AddBlogCategories(BlogMLDocument blogMlDoc, string tagGroup)
        {
            var categories = _applicationContext.Services.TagService.GetAllContentTags(tagGroup);
            foreach (var category in categories)
            {
                if (category.NodeCount == 0) continue;

                var blogMlCategory = new BlogMLCategory();
                blogMlCategory.Id = category.Id.ToString();
                blogMlCategory.CreatedOn = category.CreateDate;
                blogMlCategory.LastModifiedOn = category.UpdateDate;
                blogMlCategory.ApprovalStatus = BlogMLApprovalStatus.Approved;
                blogMlCategory.ParentId = "0";
                blogMlCategory.Title = new BlogMLTextConstruct(category.Text);
                blogMlDoc.Categories.Add(blogMlCategory);
            }
        }

        private void AddBlogAuthors(IContent authorsNode, BlogMLDocument blogMlDoc)
        {
            foreach (var author in _applicationContext.Services.ContentService.GetChildren(authorsNode.Id))
            {
                var blogMlAuthor = new BlogMLAuthor();
                blogMlAuthor.Id = author.Key.ToString();
                blogMlAuthor.CreatedOn = author.CreateDate;
                blogMlAuthor.LastModifiedOn = author.UpdateDate;
                blogMlAuthor.ApprovalStatus = BlogMLApprovalStatus.Approved;
                blogMlAuthor.Title = new BlogMLTextConstruct(author.Name);
                blogMlDoc.Authors.Add(blogMlAuthor);
            }
        }

        private void AddBlogPosts(IContent archiveNode, BlogMLDocument blogMlDoc, string tagGroup)
        {
            const int pageSize = 1000;
            var pageIndex = 0;
            IContent[] posts;
            do
            {
                long total;
                posts = _applicationContext.Services.ContentService.GetPagedChildren(archiveNode.Id, pageIndex, pageSize, out total, "createDate").ToArray();

                foreach (var child in posts)
                {
                    string content = "";
                    if (child.ContentType.Alias.InvariantEquals("ArticulateRichText"))
                    {
                        //TODO: this would also need to export all macros
                        content = child.GetValue<string>("richText");
                    }
                    else if (child.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                    {
                        content = child.GetValue<string>("markdown");
                        var markdown = new MarkdownDeep.Markdown();
                        content = markdown.Transform(content);
                    }

                    var blogMlPost = new BlogMLPost()
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
                        Url = new Uri(_umbracoContext.UrlProvider.GetUrl(child.Id), UriKind.RelativeOrAbsolute)
                    };

                    var author = blogMlDoc.Authors.FirstOrDefault(x => x.Title != null && x.Title.Content.InvariantEquals(child.GetValue<string>("author")));
                    if (author != null)
                    {
                        blogMlPost.Authors.Add(author.Id);
                    }

                    var categories = _applicationContext.Services.TagService.GetTagsForEntity(child.Id, tagGroup);
                    foreach (var category in categories)
                    {
                        blogMlPost.Categories.Add(category.Id.ToString());
                    }

                    //TODO: Tags isn't natively supported

                    blogMlDoc.AddPost(blogMlPost);
                }

                pageIndex++;
            } while (posts.Length == pageSize);
        }
    }
}