using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;

namespace Articulate
{
    public static class UrlHelperExtensions
    {
        /// <summary>
        /// Get the search url without the 'term' query string
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static string ArticulateSearchUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null ? null : model.RootBlogNode.Url.EnsureEndsWith('/') + "search";
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
            return model.RootBlogNode == null ? null : model.RootBlogNode.Url.EnsureEndsWith('/') + "categories";
        }

        /// <summary>
        /// Returns the URL for the tag list
        /// </summary>
        /// <param name="url"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static string ArticulateTagsUrl(this UrlHelper url, IMasterModel model)
        {
            return model.RootBlogNode == null ? null : model.RootBlogNode.Url.EnsureEndsWith('/') + "tags";
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
                : model.RootBlogNode.Url.EnsureEndsWith('/') + "tags/" + url.Encode(tag);
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
                : model.RootBlogNode.Url.EnsureEndsWith('/') + "categories/" + url.Encode(category);
        }
    }
}