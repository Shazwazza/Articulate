using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Articulate.Models.MetaWeblog;
using CookComputing.XmlRpc;
using HeyRed.MarkdownSharp;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using WilderMinds.MetaWeblog;

namespace Articulate.MetaWeblog
{
    public class ArticulateMetaWeblogService : IMetaWeblogProvider
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;
        private readonly IUserService _userService;
        private readonly IContentTypeService _contentTypeService;
        private readonly ILocalizationService _localizationService;
        private readonly IBackOfficeUserManager _backOfficeUserManager;
        private readonly IContentService _contentService;
        private readonly IShortStringHelper _shortStringHelper;
        private readonly ITagService _tagService;

        public ArticulateMetaWeblogService(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUserService userService,
            IContentTypeService contentTypeService,
            ILocalizationService localizationService,
            IBackOfficeUserManager backOfficeUserManager,
            IContentService contentService,
            IShortStringHelper shortStringHelper,
            ITagService tagService)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
            _userService = userService;
            _contentTypeService = contentTypeService;
            _localizationService = localizationService;
            _backOfficeUserManager = backOfficeUserManager;
            _contentService = contentService;
            _shortStringHelper = shortStringHelper;
            _tagService = tagService;
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
                throw new XmlRpcFaultException(0, "No Articulate Archive node found");
            }

            var recent = _contentService
                    .GetPagedChildren(node.Id, 0, numberOfPosts, out long totalPosts, ordering: Ordering.By("updateDate", direction: Direction.Descending))
                    .Select(FromContent)
                    .ToArray();

            return Task.FromResult(recent);
        }

        private Post FromContent(IContent post) => new Post
        {
            title = post.Name,
            postid = post.Id.ToString(CultureInfo.InvariantCulture),
            categories = post.GetValue<string>("categories").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
            dateCreated = post.UpdateDate,
            mt_excerpt = post.GetValue<string>("excerpt"),
            mt_keywords = string.Join(",", post.GetValue<string>("tags").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)),
            link = "",

            description = post.ContentType.Alias == "ArticulateRichText"           
                    ? post.GetValue<string>("richText")
                    : new Markdown().Transform(post.GetValue<string>("markdown")),

            
            permalink = post.GetValue<string>("umbracoUrlName").IsNullOrWhiteSpace()
                    ? post.Name.ToUrlSegment(_shortStringHelper)
                    : post.GetValue<string>("umbracoUrlName").ToUrlSegment(_shortStringHelper)
        };

        



        private IPublishedContent BlogRoot()
        {
            // TODO: Do not harccode 1068 - need to pass it through
            var node = _umbracoContextAccessor.GetRequiredUmbracoContext().Content.GetById(1068);

            if (node == null)
            {
                throw new XmlRpcFaultException(0, "No node found by route");
            }

            return node;
        }


        private void ValidateUser(string username, string password)
        {
            if(_backOfficeUserManager.ValidateCredentialsAsync(username, password).Result == false){
                // Throw some error if not valid credentials - so we exit out early of stuff
                throw new XmlRpcFaultException(0, "Credentials issue");
            }
        }



        public Task<string> AddPostAsync(string blogid, string username, string password, Post post, bool publish) => throw new NotImplementedException();
        public Task<int> AddCategoryAsync(string key, string username, string password, NewCategory category) => throw new NotImplementedException();
        public Task<string> AddPageAsync(string blogid, string username, string password, Page page, bool publish) => throw new NotImplementedException();
        
        public Task<bool> DeletePageAsync(string blogid, string username, string password, string pageid) => throw new NotImplementedException();
        public Task<bool> DeletePostAsync(string key, string postid, string username, string password, bool publish) => throw new NotImplementedException();

        public Task<bool> EditPageAsync(string blogid, string pageid, string username, string password, Page page, bool publish) => throw new NotImplementedException();
        public Task<bool> EditPostAsync(string postid, string username, string password, Post post, bool publish) => throw new NotImplementedException();

        public Task<Author[]> GetAuthorsAsync(string blogid, string username, string password) => throw new NotImplementedException();
        
        public Task<Page> GetPageAsync(string blogid, string pageid, string username, string password) => throw new NotImplementedException();
        public Task<Page[]> GetPagesAsync(string blogid, string username, string password, int numPages) => throw new NotImplementedException();
        public Task<Post> GetPostAsync(string postid, string username, string password) => throw new NotImplementedException();
        
        
        public Task<UserInfo> GetUserInfoAsync(string key, string username, string password) => throw new NotImplementedException();
        
        public Task<MediaObjectInfo> NewMediaObjectAsync(string blogid, string username, string password, MediaObject mediaObject) => throw new NotImplementedException();
    }
}
