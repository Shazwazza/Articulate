using System;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Routing;
using System.Xml;
using Articulate.Models;
using Articulate.Models.MetaWeblog;
using CookComputing.XmlRpc;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Security;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Articulate
{
    public class MetaWeblogHandler : XmlRpcService, IMetaWeblog, IBloggerMetaWeblog, IWordPressMetaWeblog, IRouteHandler
    {
        private readonly ApplicationContext _applicationContext;

        public MetaWeblogHandler()
            : this(ApplicationContext.Current)
        {
        }

        public MetaWeblogHandler(ApplicationContext applicationContext)
        {
            _applicationContext = applicationContext;
        }

        string IMetaWeblog.AddPost(string blogid, string username, string password, MetaWeblogPost post, bool publish)
        {
            var user = ValidateUser(username, password);

            var node = BlogRoot().Children(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive")).FirstOrDefault();
            if (node == null)
            {
                throw new XmlRpcFaultException(0, "No Articulate Archive node found");
            }

            var content = _applicationContext.Services.ContentService.CreateContent(
                post.Title, node.Id, "ArticulateRichText", user.Id);

            AddOrUpdateContent(content, post, user, publish);

            return content.Id.ToString(CultureInfo.InvariantCulture);
        }

        bool IMetaWeblog.UpdatePost(string postid, string username, string password, MetaWeblogPost post, bool publish)
        {
            var user = ValidateUser(username, password);

            var asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                return false;
            }

            //first see if it's published
            var content = _applicationContext.Services.ContentService.GetById(asInt.Result);
            if (content == null)
            {
                return false;
            }

            AddOrUpdateContent(content, post, user, publish);

            return true;
        }

        bool IBloggerMetaWeblog.DeletePost(string key, string postid, string username, string password, bool publish)
        {
            var userId = ValidateUser(username, password).Id;

            var asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                return false;
            }

            //first see if it's published
            var content = _applicationContext.Services.ContentService.GetById(asInt.Result);
            if (content == null)
            {
                return false;
            }

            //unpublish it, we won't delete it with this API
            _applicationContext.Services.ContentService.UnPublish(content, userId);

            return true;
        }

        object IMetaWeblog.GetPost(string postid, string username, string password)
        {
            ValidateUser(username, password);

            var asInt = postid.TryConvertTo<int>();
            if (!asInt)
            {
                throw new XmlRpcFaultException(0, "The id could not be parsed to an integer");
            }

            //first see if it's published
            var post = UmbracoContext.Current.ContentCache.GetById(asInt.Result);
            if (post == null)
            {
                throw new XmlRpcFaultException(0, "No post found with id " + postid);
            }

            return FromPost(new PostModel(post));

            ////TODO: Enable non-published
            //var content = _applicationContext.Services.ContentService.GetById(asInt.Result);
            //if (content == null)
            //{
            //    throw new XmlRpcFaultException(0, "No post found with id " + postid);
            //}

        }

        object[] IMetaWeblog.GetRecentPosts(string blogid, string username, string password, int numberOfPosts)
        {
            ValidateUser(username, password);

            var node = BlogRoot().Children(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive")).FirstOrDefault();
            if (node == null)
            {
                throw new XmlRpcFaultException(0, "No Articulate Archive node found");
            }

            var posts = node.Children
                .Select(x => new PostModel(x))
                .OrderByDescending(x => x.PublishedDate)
                .Take(numberOfPosts);

            return posts.Select(FromPost).ToArray();
        }

        object[] IMetaWeblog.GetCategories(string blogid, string username, string password)
        {
            ValidateUser(username, password);

            return _applicationContext.Services.TagService.GetAllTags("ArticulateCategories")                
                .Select(x => (object) new
                {
                    description = x.Text,
                    id = x.Id,
                    categoryId = x.Id,
                    title = x.Text
                }).ToArray();
        }

        object IMetaWeblog.NewMediaObject(string blogid, string username, string password, MediaObject media)
        {
            ValidateUser(username, password);

            //string path = Blog.SaveFileToDisk(media.bits, Path.GetExtension(media.name));

            //return new { url = path };

            return null;
        }

        object[] IMetaWeblog.GetUsersBlogs(string key, string username, string password)
        {
            ValidateUser(username, password);

            var node = BlogRoot();
            
            return new[]
            {
                (object) new
                {
                    blogid = node.Id,
                    blogName = node.Name,
                    url = node.UrlWithDomain()
                }
            };
        }

        object[] IBloggerMetaWeblog.GetUsersBlogs(string key, string username, string password)
        {
            return ((IMetaWeblog) this).GetUsersBlogs(key, username, password);
        }
        
        object[] IWordPressMetaWeblog.GetTags(string blogid, string username, string password)
        {
            ValidateUser(username, password);

            return _applicationContext.Services.TagService.GetAllTags("ArticulateTags")
                .Select(x => (object)new
                {
                    name = x.Text,
                    id = x.Id,
                    tag_id = x.Id                    
                }).ToArray();
        }

        private void AddOrUpdateContent(IContent content, MetaWeblogPost post, IUser user, bool publish)
        {
            content.Name = post.Title;
            content.SetValue("author", user.Name);
            if (content.HasProperty("richText"))
            {
                content.SetValue("richText", post.Content);
            }
            if (!post.Slug.IsNullOrWhiteSpace())
            {
                content.SetValue("umbracoUrlName", post.Slug);
            }
            if (!post.Excerpt.IsNullOrWhiteSpace())
            {
                content.SetValue("excerpt", post.Excerpt);
            }
            if (post.AllowComments == 1)
            {
                content.SetValue("enableComments", 1);
            }
            else if (post.AllowComments == 2)
            {
                content.SetValue("enableComments", 0);
            }
            content.SetTags("categories", post.Categories, true, "ArticulateCategories");
            var tags = post.Tags.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
            content.SetTags("tags", tags, true, "ArticulateTags");

            if (publish)
            {
                if (post.CreateDate != DateTime.MinValue)
                {
                    content.SetValue("publishedDate", post.CreateDate);    
                }

                _applicationContext.Services.ContentService.SaveAndPublishWithStatus(content, user.Id);
            }
            else
            {
                _applicationContext.Services.ContentService.Save(content, user.Id);
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
        private object FromPost(PostModel post)
        {
            return new MetaWeblogPost
            {
                AllowComments = post.EnableComments ? 1 : 2,
                Author = post.Author.Name,
                Categories = post.Categories.ToArray(),
                Content = post.Body.ToString(),
                CreateDate = post.PublishedDate != default(DateTime) ? post.PublishedDate : post.UpdateDate,
                Id = post.Id.ToString(CultureInfo.InvariantCulture),
                Slug = post.UrlName,
                Excerpt = post.Excerpt,
                Tags = string.Join(",", post.Tags.ToArray()),
                Title = post.Name
            };
        }

        //TODO: Enable non-published
        //private object FromContent(IContent post)
        //{
        //    return new
        //    {
        //        description = post.Body.ToString(),
        //        title = post.Name,
        //        pubDate = post.PublishedDate,
        //        dateCreated = post.CreateDate,
        //        date_modified = post.UpdateDate,
        //        wp_slug = post.UrlName,
        //        slug = post.UrlName,
        //        link = post.UrlWithDomain(),
        //        permaLink = post.UrlWithDomain(),
        //        postid = post.Id,
        //        excerpt = post.Excerpt,
        //        commentPolicy = post.EnableComments ? string.Empty : "0",
        //        publish = true,
        //        categories = post.Categories.ToList(),
        //        tags = post.Tags.ToList()
        //    };
        //}

        private IPublishedContent BlogRoot()
        {
            //we can determine the blog url based on the incoming request for the weblog handler
            var url = Context.Request.Url.AbsolutePath.TrimEnd("/metaweblog").EnsureStartsWith('/');
            var node = UmbracoContext.Current.ContentCache.GetByRoute(
                url, GlobalSettings.HideTopLevelNodeFromPath);

            if (node == null)
            {
                throw new XmlRpcFaultException(0, "No node found by route");
            }

            return node;
        }

        private IUser ValidateUser(string username, string password)
        {
            var provider = GetUsersMembershipProvider();

            if (!provider.ValidateUser(username, password))
            {
                throw new XmlRpcFaultException(0, "User is not valid!");
            }

            return _applicationContext.Services.UserService.GetByUsername(username);
        }

        private static MembershipProvider GetUsersMembershipProvider()
        {
            if (Membership.Providers[UmbracoConfig.For.UmbracoSettings().Providers.DefaultBackOfficeUserProvider] == null)
                throw new InvalidOperationException("No membership provider found with name " + UmbracoConfig.For.UmbracoSettings().Providers.DefaultBackOfficeUserProvider);
            else
                return Membership.Providers[UmbracoConfig.For.UmbracoSettings().Providers.DefaultBackOfficeUserProvider];
        }

        private string GetDocumentContents(HttpRequestBase request)
        {
            string documentContents;
            request.InputStream.Position = 0;
            using (var receiveStream = request.InputStream)
            {
                using (var readStream = new StreamReader(receiveStream, Encoding.UTF8))
                {
                    documentContents = readStream.ReadToEnd();
                }
            }
            return documentContents;
        }

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return this;
        }
        
    }


}

