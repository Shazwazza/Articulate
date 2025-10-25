using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Argotic.Common;
using Argotic.Syndication.Specialized;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
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
            MediaFileManager mediaFileManager,
            ArticulateTempFileSystem articulateTempFileSystem,
            IPublishedUrlProvider urlProvider,
            ISqlContext sqlContext,
            ILogger<BlogMlExporter> logger)
        {
            _mediaFileManager = mediaFileManager;
            _articulateTempFileSystem = articulateTempFileSystem;
    _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
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
            var root = _contentService?.GetById(blogRootNode) ?? throw new InvalidOperationException("No node found with id " + blogRootNode);

            if (!root.ContentType.Alias.InvariantEquals("Articulate"))
            {
                throw new InvalidOperationException("The node with id " + blogRootNode + " is not an Articulate root node");
            }

            _ = _contentTypeService?.Get("ArticulateRichText") ?? throw new InvalidOperationException("Articulate is not installed properly, the ArticulateRichText doc type could not be found");

            var categoryDataType = _dataTypeService?.GetDataType("Articulate Categories") ?? throw new InvalidOperationException("No Articulate Categories data type found");

            var categoryConfiguration = categoryDataType.ConfigurationAs<TagConfiguration>();
            var categoryGroup = categoryConfiguration?.Group;

            var tagDataType = _dataTypeService.GetDataType("Articulate Tags") ?? throw new InvalidOperationException("No Articulate Tags data type found");

            var tagConfiguration = tagDataType.ConfigurationAs<TagConfiguration>();
            var tagGroup = tagConfiguration?.Group;

            //TODO: See: http://argotic.codeplex.com/wikipage?title=Generating%20portable%20web%20log%20content&referringTitle=Home

            var blogMlDoc = new BlogMLDocument
            {
                RootUrl = new Uri(_urlProvider?.GetUrl(root.Id) ?? throw new InvalidOperationException(), UriKind.RelativeOrAbsolute),
                GeneratedOn = DateTime.Now,
                Title = new BlogMLTextConstruct(root.GetValue<string>("blogTitle")),
                Subtitle = new BlogMLTextConstruct(root.GetValue<string>("blogDescription"))
            };

            var authorsContentType = _contentTypeService.Get(ArticulateConstants.ArticulateAuthorsContentTypeAlias)
                ?? throw new InvalidOperationException($"Articulate is not installed properly, the {ArticulateConstants.ArticulateAuthorsContentTypeAlias} doc type could not be found");
            var authorsNodes = _contentService.GetPagedDescendants(root.Id, 0, int.MaxValue, out _,
                    _sqlContext?.Query<IContent>().Where(x => x.ContentTypeId == authorsContentType.Id),
                    Ordering.By("CreateDate", Direction.Descending));

            foreach (var authorsNode in authorsNodes)
            {
                AddBlogAuthors(authorsNode, blogMlDoc);
            }

            AddBlogCategories(blogMlDoc, categoryGroup);

            var archiveContentType = _contentTypeService.Get(ArticulateConstants.ArticulateArchiveContentTypeAlias)
                ?? throw new InvalidOperationException($"Articulate is not installed properly, the {ArticulateConstants.ArticulateArchiveContentTypeAlias} doc type could not be found");
            var archiveNodes = _contentService.GetPagedDescendants(root.Id, 0, int.MaxValue, out _,
                    _sqlContext?.Query<IContent>().Where(x => x.ContentTypeId == archiveContentType.Id),
                    Ordering.By("CreateDate", Direction.Descending));

            foreach (var archiveNode in archiveNodes)
            {
                AddBlogPosts(archiveNode, blogMlDoc, categoryGroup, tagGroup, exportImagesAsBase64);
            }

            WriteFile(blogMlDoc);
        }

        private void WriteFile(BlogMLDocument blogMlDoc)
        {
            using var stream = new MemoryStream();
            blogMlDoc?.Save(stream, new SyndicationResourceSaveSettings()
            {
                CharacterEncoding = Encoding.UTF8
            });
            stream.Position = 0;

            _articulateTempFileSystem?.AddFile("BlogMlExport.xml", stream, true);
        }

        private void AddBlogCategories(BlogMLDocument blogMlDoc, string tagGroup)
        {
            var categories = _tagService?.GetAllContentTags(tagGroup);
            if (categories != null)
            {
                foreach (var category in categories)
                {
                    if (category is { NodeCount: 0 })
                    {
                        continue;
                    }

                    var blogMlCategory = new BlogMLCategory
                    {
                        Id = category?.Id.ToString(),
                        CreatedOn = category?.CreateDate ?? DateTime.Now,
                        LastModifiedOn = category?.UpdateDate ?? DateTime.Now,
                        ApprovalStatus = BlogMLApprovalStatus.Approved,
                        ParentId = "0",
                        Title = new BlogMLTextConstruct(category?.Text)
                    };
                    blogMlDoc?.Categories?.Add(blogMlCategory);
                }
            }
        }

        private void AddBlogAuthors(IContent authorsNode, BlogMLDocument blogMlDoc)
        {
            if (authorsNode != null && _contentService != null)
            {
                foreach (IContent author in _contentService?.GetPagedChildren(authorsNode.Id, 0, int.MaxValue,
                             out _) ?? new List<IContent>())
                {
                    var blogMlAuthor = new BlogMLAuthor
                    {
                        Id = author?.Key.ToString(),
                        CreatedOn = author?.CreateDate ?? DateTime.Today,
                        LastModifiedOn = author?.UpdateDate ??  DateTime.Today,
                        ApprovalStatus = BlogMLApprovalStatus.Approved,
                        Title = new BlogMLTextConstruct(author?.Name)
                    };
                    blogMlDoc?.Authors?.Add(blogMlAuthor);
                }
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

                    var author = blogMlDoc.Authors.FirstOrDefault(x => x?.Title != null && x.Title.Content.InvariantEquals(child.GetValue<string>("author")));
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

                    if (child.HasProperty("postImage") && child.GetValue<string>("postImage") is { } mediaItemJson &&
                        mediaItemJson.DetectIsJson())
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(mediaItemJson);
                            var mediaKeyStr = doc.RootElement.EnumerateArray().FirstOrDefault().GetProperty("mediaKey")
                                .GetString();

                            if (Guid.TryParse(mediaKeyStr, out Guid mediaKey))
                            {
                                IMedia media = _mediaService.GetById(mediaKey);
                                if (media?.GetValue<string>(Constants.Conventions.Media.File) is { } mediaFilePath)
                                {
                                    var mime = ImageMimeType(mediaFilePath);
                                    if (!string.IsNullOrWhiteSpace(mime))
                                    {
                                        var imageUrl =
                                            new Uri(postAbsoluteUrl.GetLeftPart(UriPartial.Authority) + mediaFilePath.EnsureStartsWith('/'), UriKind.Absolute);
                                        var attachment = new BlogMLAttachment
                                        {
                                            Url = imageUrl,
                                            ExternalUri = imageUrl,
                                            IsEmbedded = exportImagesAsBase64,
                                            MimeType = mime
                                        };

                                        if (exportImagesAsBase64)
                                        {
                                            using Stream mediaFileStream = _mediaFileManager.GetFile(media, out _);
                                            using var memoryStream = new MemoryStream();
                                            mediaFileStream.CopyTo(memoryStream);
                                            attachment.Content = Convert.ToBase64String(memoryStream.ToArray());
                                        }
                                        else
                                        {
                                            attachment.Content = string.Empty;
                                        }

                                        blogMlPost.Attachments.Add(attachment);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Could not add the file to the blogML post attachments for post {PostId}", child.Id);
                        }
                    }

                    blogMlDoc.AddPost(blogMlPost);
                }

                pageIndex++;
            }
            while (posts.Length == pageSize);
        }

        private static string ImageMimeType(string src)
        {
            var ext = Path.GetExtension(src)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => null
            };
        }
    }
}
