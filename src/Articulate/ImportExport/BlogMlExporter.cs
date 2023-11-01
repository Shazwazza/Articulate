using System;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using Argotic.Common;
using Argotic.Syndication.Specialized;
using Microsoft.Extensions.Logging;
using Polly.Caching;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.Querying;
using Umbraco.Extensions;

namespace Articulate.ImportExport
{
    public class BlogMlExporter
    {
        private readonly IContentService _contentService;
        private readonly IMediaService _mediaService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataTypeService _dataTypeService;
        private readonly ITagService _tagService;
        private readonly IPublishedUrlProvider _urlProvider;
        private readonly ISqlContext _sqlContext;
        private readonly ILogger<BlogMlExporter> _logger;
        private readonly MediaFileManager _mediaFileManager;
        private readonly ArticulateTempFileSystem _articulateTempFileSystem;

        public BlogMlExporter(
            IContentService contentService,
            IMediaService mediaService,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            ITagService tagService,
            MediaFileManager mediaFileSystem,
            ArticulateTempFileSystem articulateTempFileSystem,
            IPublishedUrlProvider urlProvider,
            ISqlContext sqlContext,
            ILogger<BlogMlExporter> logger)
        {
            _mediaFileManager = mediaFileSystem;
            _articulateTempFileSystem = articulateTempFileSystem;
            _contentService = contentService;
            _mediaService = mediaService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _tagService = tagService;
            _urlProvider = urlProvider;
            _sqlContext = sqlContext;
            _logger = logger;
        }

        public void Export(
            int blogRootNode,
            bool exportImagesAsBase64 = false)
        {
            var root = _contentService.GetById(blogRootNode) ?? throw new InvalidOperationException("No node found with id " + blogRootNode);

            if (!root.ContentType.Alias.InvariantEquals("Articulate"))
            {
                throw new InvalidOperationException("The node with id " + blogRootNode + " is not an Articulate root node");
            }

            var postType = _contentTypeService.Get("ArticulateRichText") ?? throw new InvalidOperationException("Articulate is not installed properly, the ArticulateRichText doc type could not be found");

            var categoryDataType = _dataTypeService.GetDataType("Articulate Categories") ?? throw new InvalidOperationException("No Articulate Categories data type found");

            var categoryConfiguration = categoryDataType.ConfigurationAs<TagConfiguration>();
            var categoryGroup = categoryConfiguration.Group;

            var tagDataType = _dataTypeService.GetDataType("Articulate Tags") ?? throw new InvalidOperationException("No Articulate Tags data type found");

            var tagConfiguration = tagDataType.ConfigurationAs<TagConfiguration>();
            var tagGroup = tagConfiguration.Group;

            //TODO: See: http://argotic.codeplex.com/wikipage?title=Generating%20portable%20web%20log%20content&referringTitle=Home

            var blogMlDoc = new BlogMLDocument
            {
                RootUrl = new Uri(_urlProvider.GetUrl(root.Id), UriKind.RelativeOrAbsolute),
                GeneratedOn = DateTime.Now,
                Title = new BlogMLTextConstruct(root.GetValue<string>("blogTitle")),
                Subtitle = new BlogMLTextConstruct(root.GetValue<string>("blogDescription"))
            };

            var authorsContentType = _contentTypeService.Get(ArticulateConstants.ArticulateAuthorsContentTypeAlias)
                ?? throw new InvalidOperationException($"Articulate is not installed properly, the {ArticulateConstants.ArticulateAuthorsContentTypeAlias} doc type could not be found");
            var authorsNodes = _contentService.GetPagedDescendants(root.Id, 0, int.MaxValue, out var total,
                    _sqlContext.Query<IContent>().Where(x => x.ContentTypeId == authorsContentType.Id),
                    Ordering.By("CreateDate", Umbraco.Cms.Core.Direction.Descending));

            foreach (var authorsNode in authorsNodes)
            {
                AddBlogAuthors(authorsNode, blogMlDoc);
            }

            AddBlogCategories(blogMlDoc, categoryGroup);

            var archiveContentType = _contentTypeService.Get(ArticulateConstants.ArticulateArchiveContentTypeAlias)
                ?? throw new InvalidOperationException($"Articulate is not installed properly, the {ArticulateConstants.ArticulateArchiveContentTypeAlias} doc type could not be found");
            var archiveNodes = _contentService.GetPagedDescendants(root.Id, 0, int.MaxValue, out total,
                    _sqlContext.Query<IContent>().Where(x => x.ContentTypeId == archiveContentType.Id),
                    Ordering.By("CreateDate", Umbraco.Cms.Core.Direction.Descending));

            foreach (var archiveNode in archiveNodes)
            {
                AddBlogPosts(archiveNode, blogMlDoc, categoryGroup, tagGroup, exportImagesAsBase64);
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

                _articulateTempFileSystem.AddFile("BlogMlExport.xml", stream, true);
            }
        }

        private void AddBlogCategories(BlogMLDocument blogMlDoc, string tagGroup)
        {
            var categories = _tagService.GetAllContentTags(tagGroup);
            foreach (var category in categories)
            {
                if (category.NodeCount == 0)
                {
                    continue;
                }

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

        private void AddBlogPosts(IContent archiveNode, BlogMLDocument blogMlDoc, string categoryGroup, string tagGroup, bool exportImagesAsBase64)
        {
            // TODO: This won't work for variants
            const int pageSize = 1000;
            var pageIndex = 0;
            IContent[] posts;
            do
            {
                posts = _contentService.GetPagedChildren(archiveNode.Id, pageIndex, pageSize, out long _, ordering: Ordering.By("createDate")).ToArray();

                foreach (var child in posts)
                {
                    if (!child.Published)
                    {
                        continue;
                    }

                    string content = "";
                    if (child.ContentType.Alias.InvariantEquals("ArticulateRichText"))
                    {
                        //TODO: this would also need to export all macros
                        content = child.GetValue<string>("richText");
                    }
                    else if (child.ContentType.Alias.InvariantEquals("ArticulateMarkdown"))
                    {
                        content = MarkdownHelper.ToHtml(child.GetValue<string>("markdown"));
                    }

                    var postUrl = new Uri(_urlProvider.GetUrl(child.Id), UriKind.RelativeOrAbsolute);
                    var postAbsoluteUrl = new Uri(_urlProvider.GetUrl(child.Id, UrlMode.Absolute), UriKind.Absolute);
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

                    var tags = _tagService.GetTagsForEntity(child.Id, tagGroup).Select(t => t.Text).ToList();
                    if (tags?.Any() == true)
                    {
                        blogMlPost.AddExtension(
                            new Syndication.BlogML.TagsSyndicationExtension()
                            {
                                Context = { Tags = new Collection<string>(tags) }
                            });
                    }

                    //add the image attached if there is one
                    if (child.HasProperty("postImage"))
                    {
                        try
                        {
                            var mediaUdi = child.GetValue<string>("postImage");

                            if (!string.IsNullOrWhiteSpace(mediaUdi))
                            {
                                var udi = (GuidUdi)UdiParser.Parse(mediaUdi);
                                var media = _mediaService.GetById(udi.Guid)
                                    ?? throw new InvalidOperationException("No media found by id " + udi);

                                var mediaPath = _mediaFileManager.GetMediaPath(
                                    media.GetValue(Constants.Conventions.Media.File).ToString(),
                                    media.Key,
                                    media.Properties[Constants.Conventions.Media.File].PropertyType.Key);

                                var mime = BlogMlExporter.ImageMimeType(mediaPath);

                                if (!mime.IsNullOrWhiteSpace())
                                {
                                    var imageUrl = new Uri(postAbsoluteUrl.GetLeftPart(UriPartial.Authority) + mediaPath.EnsureStartsWith('/'), UriKind.Absolute);

                                    if (exportImagesAsBase64)
                                    {
                                        using (var mediaFileStream = _mediaFileManager.GetFile(media, out _))
                                        {
                                            byte[] bytes;
                                            using (var memoryStream = new MemoryStream())
                                            {
                                                mediaFileStream.CopyTo(memoryStream);
                                                bytes = memoryStream.ToArray();
                                            }

                                            blogMlPost.Attachments.Add(new BlogMLAttachment
                                            {
                                                Content = Convert.ToBase64String(bytes),
                                                Url = imageUrl,
                                                ExternalUri = imageUrl,
                                                IsEmbedded = true,
                                                MimeType = mime
                                            });
                                        }
                                    }
                                    else
                                    {
                                        blogMlPost.Attachments.Add(new BlogMLAttachment
                                        {
                                            Content = string.Empty,
                                            Url = imageUrl,
                                            ExternalUri = imageUrl,
                                            IsEmbedded = false,
                                            MimeType = mime
                                        });
                                    }
                                }
                            }                            
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Could not add the file to the blogML post attachments");
                        }
                    }

                    blogMlDoc.AddPost(blogMlPost);
                }

                pageIndex++;
            } while (posts.Length == pageSize);
        }

        private static string ImageMimeType(string src)
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
