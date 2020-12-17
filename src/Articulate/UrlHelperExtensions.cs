using System.Linq;
using Articulate.Models;
using System.Web;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Web;
using RouteCollectionExtensions = Articulate.Routing.RouteCollectionExtensions;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate
{
    public static class UrlHelperExtensions
    {
        /// <summary>
        /// Returns the url of a themed asset
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <param name="relativeAssetPath"></param>
        /// <returns></returns>
        public static string ThemedAsset(this UrlHelper url, IMasterModel model, string relativeAssetPath)
        {
            return VirtualPathUtility.ToAbsolute(PathHelper.GetThemePath(model)).EnsureEndsWith('/') + "assets/" + relativeAssetPath;
        }

        /// <summary>
        /// Returns the main rss feed url for this blog
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static string ArticulateRssUrl(this UrlHelper url, IMasterModel model)
        {
            return model.CustomRssFeed.IsNullOrWhiteSpace()
                ? model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "rss"
                : model.CustomRssFeed;
        }

        public static string ArticulateCreateBlogEntryUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null
                ? null
                : model.RootBlogNode.Url.EnsureEndsWith('/') + "a-new/";
        }

        /// <summary>
        /// Returns an RSS feed URL specific to this tag
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static string ArticulateTagRssUrl(this UrlHelper url, PostsByTagModel model)
        {
            return model.TagUrl.EnsureEndsWith('/') + "rss";
        }

        /// <summary>
        /// Returns an RSS feed URL specific to this author
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static string ArticulateAuthorRssUrl(this UrlHelper url, AuthorModel model)
        {
            var articulateRootUriPath = RouteCollectionExtensions.RoutePathFromNodeUrl(model.RootBlogNode.Url);
            var routeHash = articulateRootUriPath.GetHashCode();
            var routeName = "articulate_author_rss_" + routeHash;
            return url.RouteUrl(routeName, new
            {                
                authorId = model.Id.ToInvariantString()
            });
        }

        /// <summary>
        /// Get the search url without the 'term' query string
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <param name="includeDomain"></param>
        /// <returns></returns>
        public static string ArticulateSearchUrl(this UrlHelper url, IMasterModel model, bool includeDomain = false)
        {
            return model.RootBlogNode == null
                ? null
                : (includeDomain
                      ? model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/')
                      : model.RootBlogNode.Url.EnsureEndsWith('/')) +
                  model.RootBlogNode.Value<string>("searchUrlName");
        }

        /// <summary>
        /// The Home Blog Url
        /// </summary>
        public static string ArticulateRootUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode?.Url;
        }

        /// <summary>
        /// Returns the default categories list URL for blog posts
        /// </summary>
        public static string ArticulateCategoriesUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null
                ? null
                : model.RootBlogNode.Url.EnsureEndsWith('/') +
                  model.RootBlogNode.Value<string>("categoriesUrlName");
        }

        /// <summary>
        /// Returns the authors list URL
        /// </summary>
        public static string ArticulateAuthorsUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode?.ChildrenOfType(ArticulateConstants.ArticulateAuthorsContentTypeAlias).FirstOrDefault()?.Url;
        }

        /// <summary>
        /// Returns the URL for the tag list
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static string ArticulateTagsUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null
                ? null
                : model.RootBlogNode.Url.EnsureEndsWith('/') +
                  model.RootBlogNode.Value<string>("tagsUrlName");
        }

        /// <summary>
        /// Returns the url for a single tag
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static string ArticulateTagUrl(this UrlHelper url, IMasterModel model, string tag)
        {
            return model.RootBlogNode == null
                ? null
                : model.RootBlogNode.Url.EnsureEndsWith('/') +
                  model.RootBlogNode.Value<string>("tagsUrlName").EnsureEndsWith('/') +
                  tag.SafeEncodeUrlSegments();
        }

        /// <summary>
        /// Returns the url for a single category
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <param name="category"></param>
        /// <returns></returns>
        public static string ArticulateCategoryUrl(this UrlHelper url, IMasterModel model, string category)
        {
            return model.RootBlogNode == null
                ? null
                : model.RootBlogNode.Url.EnsureEndsWith('/') +
                  model.RootBlogNode.Value<string>("categoriesUrlName").EnsureEndsWith('/') +
                  category.SafeEncodeUrlSegments();
        }
    }
}
