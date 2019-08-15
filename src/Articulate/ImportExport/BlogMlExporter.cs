using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Argotic.Common;
using Argotic.Syndication.Specialized;
using HeyRed.MarkdownSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate.ImportExport
{
    public class BlogMlExporter
    {
        private readonly ArticulateTempFileSystem _fileSystem;
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataTypeService _dataTypeService;
        private readonly ITagService _tagService;
        private readonly ILogger _logger;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public BlogMlExporter(IUmbracoContextAccessor umbracoContextAccessor, ArticulateTempFileSystem fileSystem, IContentService contentService, IContentTypeService contentTypeService, IDataTypeService dataTypeService, ITagService tagService, ILogger logger)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _fileSystem = fileSystem;
            _contentService = contentService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _tagService = tagService;
            _logger = logger;
        }

        public void Export(
            string fileName,
            int blogRootNode)
        {
            var root = _contentService.GetById(blogRootNode);
            if (root == null)
            {
                throw new InvalidOperationException("No node found with id " + blogRootNode);
            }
            if (!root.ContentType.Alias.InvariantEquals("Articulate"))
            {
                throw new InvalidOperationException("The node with id " + blogRootNode + " is not an Articulate root node");
            }

            var postType = _contentTypeService.Get("ArticulateRichText");
            if (postType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateRichText doc type could not be found");
            }
            
            var archiveContentType = _contentTypeService.Get("ArticulateArchive");
            var archiveNodes = _contentService.GetPagedOfType(archiveContentType.Id, 0, int.MaxValue, out long totalArchive, null);

            var authorsContentType = _contentTypeService.Get("ArticulateAuthors");
            var authorsNodes = _contentService.GetPagedOfType(authorsContentType.Id, 0, int.MaxValue, out long totalAuthors, null);

            if (totalArchive == 0)
            {
                throw new InvalidOperationException("No ArticulateArchive found under the blog root node");
            }

            if (totalAuthors == 0)
            {
                throw new InvalidOperationException("No ArticulateAuthors found under the blog root node");
            }

            var categoryDataType = _dataTypeService.GetDataType("Articulate Categories");
            if (categoryDataType == null)
            {
                throw new InvalidOperationException("No Articulate Categories data type found");
            }            
            var categoryConfiguration = categoryDataType.ConfigurationAs<TagConfiguration>();
            var categoryGroup = categoryConfiguration.Group;

            var tagDataType = _dataTypeService.GetDataType("Articulate Tags");
            if (tagDataType == null)
            {
                throw new InvalidOperationException("No Articulate Tags data type found");
            }
            var tagConfiguration = tagDataType.ConfigurationAs<TagConfiguration>();
            var tagGroup = tagConfiguration.Group;

            //TODO: See: http://argotic.codeplex.com/wikipage?title=Generating%20portable%20web%20log%20content&referringTitle=Home

            var blogMlDoc = new BlogMLDocument
            {
                RootUrl = new Uri(_umbracoContextAccessor.UmbracoContext.UrlProvider.GetUrl(root.Id), UriKind.RelativeOrAbsolute),
                GeneratedOn = DateTime.Now,
                Title = new BlogMLTextConstruct(root.GetValue<string>("blogTitle")),
                Subtitle = new BlogMLTextConstruct(root.GetValue<string>("blogDescription"))
            };

            foreach (var authorsNode in authorsNodes)
            {
                AddBlogAuthors(authorsNode, blogMlDoc);
            }
            AddBlogCategories(blogMlDoc, categoryGroup);
            foreach (var archiveNode in archiveNodes)
            {
                AddBlogPosts(archiveNode, blogMlDoc, categoryGroup, tagGroup);
            }
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
            var categories = _tagService.GetAllContentTags(tagGroup);
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
            foreach (var author in _contentService.GetPagedChildren(authorsNode.Id, 0, int.MaxValue, out long totalAuthors))
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

        private void AddBlogPosts(IContent archiveNode, BlogMLDocument blogMlDoc, string categoryGroup, string tagGroup)
        {
            const int pageSize = 1000;
            var pageIndex = 0;
            IContent[] posts;
            do
            {
                posts = _contentService.GetPagedChildren(archiveNode.Id, pageIndex, pageSize, out long _ , ordering: Ordering.By("createDate")).ToArray();

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
                        var md = new Markdown();
                        content = md.Transform(child.GetValue<string>("markdown"));
                    }

                    var postUrl = new Uri(_umbracoContextAccessor.UmbracoContext.UrlProvider.GetUrl(child.Id), UriKind.RelativeOrAbsolute);
                    var postAbsoluteUrl = new Uri(_umbracoContextAccessor.UmbracoContext.UrlProvider.GetUrl(child.Id, UrlMode.Absolute), UriKind.Absolute);
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
                        Url = postUrl
                    };

                    var author = blogMlDoc.Authors.FirstOrDefault(x => x.Title != null && x.Title.Content.InvariantEquals(child.GetValue<string>("author")));
                    if (author != null)
                    {
                        blogMlPost.Authors.Add(author.Id);
                    }

                    var categories = _tagService.GetTagsForEntity(child.Id, categoryGroup);

                    foreach (var category in categories)
                    {
                        blogMlPost.Categories.Add(category.Id.ToString());
                    }

                    var tags = _tagService.GetTagsForEntity(child.Id, tagGroup).Select(t =>t.Text).ToList();
                    if (tags?.Any() == true)
                    {
                        blogMlPost.AddExtension(
                            new Syndication.BlogML.TagsSyndicationExtension()
                            {
                                Context = {Tags = new Collection<string>(tags)}
                            });
                    }

                    //add the image attached if there is one
                    if (child.HasProperty("postImage"))
                    {
                        try
                        {
                            var val = child.GetValue<string>("postImage");
                            var json = JsonConvert.DeserializeObject<JObject>(val);
                            var src = json.Value<string>("src");

                            var mime = ImageMimeType(src);

                            if (!mime.IsNullOrWhiteSpace())
                            {
                                var imageUrl = new Uri(postAbsoluteUrl.GetLeftPart(UriPartial.Authority) + src.EnsureStartsWith('/'), UriKind.Absolute);
                                blogMlPost.Attachments.Add(new BlogMLAttachment
                                {
                                    Content = string.Empty, //this is used for embedded resources
                                    Url = imageUrl,
                                    ExternalUri = imageUrl,
                                    IsEmbedded = false,
                                    MimeType = mime
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error<BlogMlExporter>(ex, "Could not add the file to the blogML post attachments");
                        }
                    }

                    

                    blogMlDoc.AddPost(blogMlPost);
                }

                pageIndex++;
            } while (posts.Length == pageSize);
        }

        private string ImageMimeType(string src)
        {
            var ext = Path.GetExtension(src)?.ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                default:
                    return null;
            }
        }
    }
}
