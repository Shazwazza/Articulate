using System.Web;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;

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
                ? model.RootBlogNode.UrlWithDomain().EnsureEndsWith('/') + "rss"
                : model.CustomRssFeed;
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
        /// Get the search url without the 'term' query string
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static string ArticulateSearchUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null
                ? null
                : model.RootBlogNode.Url.EnsureEndsWith('/') +
                  model.RootBlogNode.GetPropertyValue<string>("searchUrlName");
        }

        /// <summary>
        /// The Home Blog Url
        /// </summary>
        public static string ArticulateRootUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null ? null : model.RootBlogNode.Url;
        }

        /// <summary>
        /// Returns the default categories list URL for blog posts
        /// </summary>
        public static string ArticulateCategoriesUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null
                ? null
                : model.RootBlogNode.Url.EnsureEndsWith('/') +
                  model.RootBlogNode.GetPropertyValue<string>("categoriesUrlName");
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
                  model.RootBlogNode.GetPropertyValue<string>("tagsUrlName");
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
                  model.RootBlogNode.GetPropertyValue<string>("tagsUrlName").EnsureEndsWith('/') +
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
                  model.RootBlogNode.GetPropertyValue<string>("categoriesUrlName").EnsureEndsWith('/') +
                  category.SafeEncodeUrlSegments();
        }
    }
}