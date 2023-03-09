using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Articulate.Models;
using HeyRed.MarkdownSharp;
using Newtonsoft.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using WilderMinds.MetaWeblog;

namespace Articulate.MetaWeblog
{
    public class ArticulateMetaWeblogProvider : IMetaWeblogProvider
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IUserService _userService;
        private readonly IContentTypeService _contentTypeService;
        private readonly ILocalizationService _localizationService;
        private readonly IBackOfficeUserManager _backOfficeUserManager;
        private readonly IContentService _contentService;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly IDataTypeService _dataTypeService;
        private readonly PropertyEditorCollection _propertyEditors;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly MediaFileManager _mediaFileManager;
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IVariationContextAccessor _variationContextAccessor;
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly ITagService _tagService;
        private readonly int _articulateBlogRootNodeId;
        private readonly Regex _mediaSrc = new Regex(" src=(?:\"|')(?:http|https)://(?:[\\w\\d:/-]+?)(articulate/.*?)(?:\"|')", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly Regex _mediaHref = new Regex(" href=(?:\"|')(?:http|https)://(?:[\\w\\d:/-]+?)(articulate/.*?)(?:\"|')", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ArticulateMetaWeblogProvider(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUserService userService,
            IContentTypeService contentTypeService,
            ILocalizationService localizationService,
            IBackOfficeUserManager backOfficeUserManager,
            IContentService contentService,
            IShortStringHelper shortStringHelper,
            IDataTypeService dataTypeService,
            PropertyEditorCollection propertyEditors,
            IJsonSerializer jsonSerializer,
            MediaFileManager mediaFileManager,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            IImageUrlGenerator imageUrlGenerator,
            ITagService tagService,
            int articulateBlogRootNodeId)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _userService = userService;
            _contentTypeService = contentTypeService;
            _localizationService = localizationService;
            _backOfficeUserManager = backOfficeUserManager;
            _contentService = contentService;
            _shortStringHelper = shortStringHelper;
            _dataTypeService = dataTypeService;
            _propertyEditors = propertyEditors;
            _jsonSerializer = jsonSerializer;
            _mediaFileManager = mediaFileManager;
            _publishedValueFallback = publishedValueFallback;
            _variationContextAccessor = variationContextAccessor;
            _imageUrlGenerator = imageUrlGenerator;
            _tagService = tagService;
            _articulateBlogRootNodeId = articulateBlogRootNodeId;
        }

        public Task<BlogInfo[]> GetUsersBlogsAsync(string key, string username, string password)
        {
            ValidateUser(username, password);

            var node = BlogRoot();
            var blogs = new BlogInfo[]
            {
                new BlogInfo()
                {
                    blogid = node.Id.ToString(),
                    blogName = node.Name,
                    url = node.Url()
                }
            };

            return Task.FromResult(blogs);
        }

        public Task<CategoryInfo[]> GetCategoriesAsync(string blogid, string username, string password)
        {
            ValidateUser(username, password);

            // TODO: These would be across all Articulate Blog root nodes :S
            var tags = _tagService.GetAllTags("ArticulateCategories")
                .Select(x => new CategoryInfo()
                {
                    title = x.Text,
                    categoryid = x.Id.ToString()

                    // TODO HTML & RSS URL ? (Wasnt used before)
                }).ToArray();

            return Task.FromResult(tags);
        }

        public Task<WilderMinds.MetaWeblog.Tag[]> GetTagsAsync(string blogid, string username, string password)
        {
            ValidateUser(username, password);

            // TODO: These would be across all Articulate Blog root nodes :S
            var tags = _tagService.GetAllTags("ArticulateTags")
                .Select(x => new WilderMinds.MetaWeblog.Tag()
                {
                    name = x.Text
                })
                .ToArray();

            return Task.FromResult(tags);
        }

        public Task<Post[]> GetRecentPostsAsync(string blogid, string username, string password, int numberOfPosts)
        {
            ValidateUser(username, password);

            var node = BlogRoot().ChildrenOfType(ArticulateConstants.ArticulateArchiveContentTypeAlias).FirstOrDefault();
            if (node == null)
            {
                throw new InvalidOperationException("No Articulate Archive node found");
            }

            var recent = _contentService
                    .GetPagedChildren(node.Id, 0, numberOfPosts, out long totalPosts, ordering: Ordering.By("updateDate", direction: Direction.Descending))
                    .Select(FromContent)
                    .ToArray();

            return Task.FromResult(recent);
        }

        public Task<string> AddPostAsync(string blogid, string username, string password, Post post, bool publish)
        {
            var user = ValidateUser(username, password);

            var root = BlogRoot();

            var node = root.ChildrenOfType(ArticulateConstants.ArticulateArchiveContentTypeAlias).FirstOrDefault();
            if (node == null)
            {
                throw new InvalidOperationException("No Articulate Archive node found");
            }

            var contentType = _contentTypeService.Get("ArticulateRichText");
            if (contentType == null)
            {
                throw new InvalidOperationException("No content type found with alias 'ArticulateRichText'");
            }

            var content = _contentService.CreateWithInvariantOrDefaultCultureName(
                post.title, node.Id, contentType, _localizationService, user.Id);

            var extractFirstImageAsProperty = false;
            if (root.HasProperty("extractFirstImage"))
            {
                extractFirstImageAsProperty = root.Value<bool>("extractFirstImage");
            }

            AddOrUpdateContent(content, contentType, post, user, publish, extractFirstImageAsProperty);

            return Task.FromResult(content.Id.ToString(CultureInfo.InvariantCulture));

        }

        public Task<bool> DeletePostAsync(string key, string postid, string username, string password, bool publish)
        {
            var user = ValidateUser(username, password);
            var userId = user.Id;

            var asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                return Task.FromResult(false);
            }

            //first see if it's published
            var content = _contentService.GetById(asInt.Result);
            if (content == null)
            {
                return Task.FromResult(false);
            }

            // Put in recylce bin - rather than unpublish
            _contentService.MoveToRecycleBin(content, userId);
            return Task.FromResult(true);
        }

        public Task<Post> GetPostAsync(string postid, string username, string password)
        {
            ValidateUser(username, password);

            var asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                throw new InvalidOperationException("The id could not be parsed to an integer");
            }

            //first see if it's published
            var post = _umbracoContextAccessor.GetRequiredUmbracoContext().Content.GetById(asInt.Result);
            if (post != null)
            {
                var fromPost = FromPost(new PostModel(post, _publishedValueFallback, _variationContextAccessor, _imageUrlGenerator));
                return Task.FromResult(fromPost);
            }

            var content = _contentService.GetById(asInt.Result);
            if (content == null)
            {
                throw new InvalidOperationException("No post found with id " + postid);
            }

            var fromContent = FromContent(content);
            return Task.FromResult(fromContent);
        }

        public Task<MediaObjectInfo> NewMediaObjectAsync(string blogid, string username, string password, MediaObject mediaObject)
        {
            ValidateUser(username, password);

            var bytes = Convert.FromBase64String(mediaObject.bits);

            // Save File
            using (var ms = new MemoryStream(bytes))
            {
                var fileUrl = "articulate/" + mediaObject.name.ToSafeFileName(_shortStringHelper);
                _mediaFileManager.FileSystem.AddFile(fileUrl, ms);
                var absUrl = _mediaFileManager.FileSystem.GetUrl(fileUrl);

                var result = new MediaObjectInfo()
                {
                    url = absUrl
                };

                return Task.FromResult(result);
            }
        }

        public Task<bool> EditPostAsync(string postid, string username, string password, Post post, bool publish)
        {
            var user = ValidateUser(username, password);

            var asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                throw new InvalidOperationException("The id could not be parsed to an integer");
            }

            var umbracoContent = _contentService.GetById(asInt.Result);

            var contentType = _contentTypeService.Get("ArticulateRichText");
            if (contentType == null)
            {
                throw new InvalidOperationException("No content type found with alias 'ArticulateRichText'");
            }

            var root = BlogRoot();

            var extractFirstImageAsProperty = false;
            if (root.HasProperty("extractFirstImage"))
            {
                extractFirstImageAsProperty = root.Value<bool>("extractFirstImage");
            }

            AddOrUpdateContent(umbracoContent, contentType, post, user, publish, extractFirstImageAsProperty);

            // Bool - assume to notify if published with new updates
            return Task.FromResult(true);
        }

        // Seems these are not used/supported
        public Task<int> AddCategoryAsync(string key, string username, string password, NewCategory category) => throw new NotImplementedException();
        public Task<Author[]> GetAuthorsAsync(string blogid, string username, string password) => throw new NotImplementedException();
        public Task<UserInfo> GetUserInfoAsync(string key, string username, string password) => throw new NotImplementedException();

        // Not supporting pages from the WordPress implementation
        public Task<string> AddPageAsync(string blogid, string username, string password, Page page, bool publish) => throw new NotImplementedException();
        public Task<bool> EditPageAsync(string blogid, string pageid, string username, string password, Page page, bool publish) => throw new NotImplementedException();
        public Task<bool> DeletePageAsync(string blogid, string username, string password, string pageid) => throw new NotImplementedException();
        public Task<Page> GetPageAsync(string blogid, string pageid, string username, string password) => throw new NotImplementedException();
        public Task<Page[]> GetPagesAsync(string blogid, string username, string password, int numPages) => throw new NotImplementedException();

        private void AddOrUpdateContent(IContent content, IContentType contentType, Post post, IUser user, bool publish, bool extractFirstImageAsProperty)
        {
            content.SetInvariantOrDefaultCultureName(post.title, contentType, _localizationService);

            content.SetInvariantOrDefaultCultureValue("author", user.Name, contentType, _localizationService);
            if (content.HasProperty("richText"))
            {
                var firstImage = "";

                // Extract the articulate firstImage.
                // Re-update the URL to be the one from the media file system.
                // Live writer will always make the urls absolute even if we return a relative path from NewMediaObject
                // so we will re-update it. If it's the default media file system then this will become a relative path
                // which is what we want, if it's a custom file system it will update it to it's absolute path.
                var contentToSave = _mediaSrc.Replace(post.description, match =>
                {
                    if (match.Groups.Count == 2)
                    {
                        var relativePath = match.Groups[1].Value;
                        var mediaFileSystemPath = _mediaFileManager.FileSystem.GetUrl(relativePath);
                        if (firstImage.IsNullOrWhiteSpace())
                        {
                            // get the first images absolute media path                            
                            firstImage = mediaFileSystemPath;
                        }

                        return " src=\"" + mediaFileSystemPath + "\"";
                    }

                    return null;
                });

                var imagesProcessed = 0;

                // Now ensure all anchors have the custom class
                // and the media file system path is re-updated as per above
                contentToSave = _mediaHref.Replace(contentToSave, match =>
                {
                    if (match.Groups.Count == 2)
                    {
                        var relativePath = match.Groups[1].Value;
                        var mediaFileSystemPath = _mediaFileManager.FileSystem.GetUrl(relativePath);

                        var href = " href=\"" +
                               mediaFileSystemPath +
                               "\" class=\"a-image-" + imagesProcessed + "\" ";

                        imagesProcessed++;

                        return href;
                    }

                    return null;
                });

                content.SetInvariantOrDefaultCultureValue("richText", contentToSave, contentType, _localizationService);
                if (extractFirstImageAsProperty
                    && content.HasProperty("postImage")
                        && !firstImage.IsNullOrWhiteSpace())
                {
                    var configuration = _dataTypeService.GetDataType(content.Properties["postImage"].PropertyType.DataTypeId).ConfigurationAs<ImageCropperConfiguration>();
                    var crops = configuration?.Crops ?? Array.Empty<ImageCropperConfiguration.Crop>();

                    var imageCropValue = new ImageCropperValue
                    {
                        Src = firstImage,
                        Crops = crops.Select(x => new ImageCropperValue.ImageCropperCrop
                        {
                            Alias = x.Alias,
                            Height = x.Height,
                            Width = x.Width
                        }).ToList()
                    };

                    content.SetInvariantOrDefaultCultureValue(
                        "postImage",
                        JsonConvert.SerializeObject(imageCropValue),
                        contentType,
                        _localizationService);
                }
            }

            if (!post.link.IsNullOrWhiteSpace())
            {
                content.SetInvariantOrDefaultCultureValue("umbracoUrlName", post.link, contentType, _localizationService);
            }

            if (!post.mt_excerpt.IsNullOrWhiteSpace())
            {
                content.SetInvariantOrDefaultCultureValue("excerpt", post.mt_excerpt, contentType, _localizationService);
            }

            // TODO: This will be avilable when this PR is merged: https://github.com/shawnwildermuth/MetaWeblog/pull/16
            //if (post.AllowComments == 1)
            //{
            //    content.SetInvariantOrDefaultCultureValue("enableComments", 1, contentType, _localizationService);
            //}
            //else if (post.AllowComments == 2)
            //{
            //    content.SetInvariantOrDefaultCultureValue("enableComments", 0, contentType, _localizationService);
            //}

            content.AssignInvariantOrDefaultCultureTags("categories", post.categories, contentType, _localizationService, _dataTypeService, _propertyEditors, _jsonSerializer);
            var tags = post.mt_keywords
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Distinct()
                .ToArray();

            content.AssignInvariantOrDefaultCultureTags("tags", tags, contentType, _localizationService, _dataTypeService, _propertyEditors, _jsonSerializer);

            if (publish)
            {
                if (post.dateCreated != DateTime.MinValue)
                {
                    content.SetInvariantOrDefaultCultureValue("publishedDate", post.dateCreated, contentType, _localizationService);
                }

                _contentService.SaveAndPublish(content, userId: user.Id);
            }
            else
            {
                _contentService.Save(content, user.Id);
            }
        }

        /// <summary>
        /// There are so many variants of Metaweblog API so I've just included as many properties, custom ones, etc... that i can find
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        /// <remarks>
        /// http://msdn.microsoft.com/en-us/library/bb463260.aspx
        /// http://xmlrpc.scripting.com/metaWeblogApi.html
        /// http://cyber.law.harvard.edu/rss/rss.html#hrelementsOfLtitemgt
        /// http://codex.wordpress.org/XML-RPC_MetaWeblog_API
        /// https://blogengine.codeplex.com/SourceControl/latest#BlogEngine/BlogEngine.Core/API/MetaWeblog/MetaWeblogHandler.cs
        /// </remarks>
        private Post FromPost(PostModel post) => new Post
        {
            categories = post.Categories.ToArray(),
            description = post.Body.ToString(),
            dateCreated = post.PublishedDate != default(DateTime) ? post.PublishedDate : post.UpdateDate,
            postid = post.Id.ToString(CultureInfo.InvariantCulture),
            wp_slug = post.Url(),
            mt_excerpt = post.Excerpt,
            mt_keywords = string.Join(",", post.Tags.ToArray()),
            title = post.Name
        };

        private Post FromContent(IContent post) => new Post
        {
            title = post.Name,
            postid = post.Id.ToString(CultureInfo.InvariantCulture),
            dateCreated = post.UpdateDate,
            mt_excerpt = post.GetValue<string>("excerpt"),
            link = "",

            mt_keywords = string.IsNullOrWhiteSpace(post.GetValue<string>("tags")) == false
            ? string.Join(",", post.GetValue<string>("tags").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            : string.Empty,

            categories = string.IsNullOrEmpty(post.GetValue<string>("categories")) == false
            ? post.GetValue<string>("categories").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>(),

            description = post.ContentType.Alias == "ArticulateRichText"
            ? post.GetValue<string>("richText")
            : new Markdown().Transform(post.GetValue<string>("markdown")),

            permalink = post.GetValue<string>("umbracoUrlName").IsNullOrWhiteSpace()
            ? post.Name.ToUrlSegment(_shortStringHelper)
            : post.GetValue<string>("umbracoUrlName").ToUrlSegment(_shortStringHelper)
        };

        private IPublishedContent BlogRoot()
        {
            var node = _umbracoContextAccessor.GetRequiredUmbracoContext().Content.GetById(_articulateBlogRootNodeId);

            if (node == null)
            {
                throw new InvalidOperationException("No node found by route");
            }

            return node;
        }

        private IUser ValidateUser(string username, string password)
        {
            if (_backOfficeUserManager.ValidateCredentialsAsync(username, password).Result == false)
            {
                // Throw some error if not valid credentials - so we exit out early of stuff
                throw new InvalidOperationException("Credentials issue");
            }

            return _userService.GetByUsername(username);
        }
    }
}
