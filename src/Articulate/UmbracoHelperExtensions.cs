using Articulate.Models;
using Articulate.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;

namespace Articulate
{

    public static class UmbracoHelperExtensions
    {
        /// <summary>
        /// A method that will return number of posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="articulateArchiveIds"></param>
        /// <returns></returns>
        public static int GetPostCount(this UmbracoHelper helper, params int[] articulateArchiveIds)
        {
            var totalPosts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants())
                .Count();

            return totalPosts;
        }

        /// <summary>
        /// A method that will return number of posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="authorName"></param>
        /// <param name="articulateArchiveIds"></param>
        /// <returns></returns>
        public static int GetPostCount(this UmbracoHelper helper, string authorName, params int[] articulateArchiveIds)
        {
            var totalPosts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants().Where(d => d.Value<string>("author") == authorName))
                .Count();

            return totalPosts;
        }

        /// <summary>
        /// A method that will return the posts sorted by published date in an efficient way
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="articulateArchiveIds"></param>
        /// <param name="pager"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static IEnumerable<IPublishedContent> GetPostsSortedByPublishedDate(
            this UmbracoHelper helper, 
            PagerModel pager,
            Func<IPublishedContent, bool> filter,
            params int[] articulateArchiveIds)
        {
            var posts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants());
            
            //apply a filter if there is one
            if (filter != null)
            {
                posts = posts.Where(filter);
            }

            //now do the ordering
            posts = posts.OrderByDescending(x => x.Value<DateTime>("publishedDate"))
                .Skip(pager.CurrentPageIndex * pager.PageSize)
                .Take(pager.PageSize);

            return posts;
        }

        public static PostTagCollection GetPostTagCollection(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            ITagQuery tagQuery,
            ArticulateTagService articulateTagService)
        {
            var tagsBaseUrl = masterModel.RootBlogNode.Value<string>("tagsUrlName");

            IEnumerable<PostsByTagModel> contentByTags = articulateTagService.GetContentByTags(
                helper,
                tagQuery,
                masterModel,
                "ArticulateTags",
                tagsBaseUrl);

            return new PostTagCollection(contentByTags);
        }

        

        /// <summary>
        /// Returns a list of the most recent posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public static IEnumerable<PostModel> GetRecentPosts(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            int count,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor)
        {
            var listNodes = GetListNodes(masterModel);

            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            var pager = new PagerModel(count, 0, 1);

            var listItems = helper.GetPostsSortedByPublishedDate(pager, null, listNodeIds);

            var rootPageModel = new ListModel(listNodes[0], pager, listItems, publishedValueFallback, variationContextAccessor);
            return rootPageModel.Posts;
        }

        /// <summary>
        /// Returns a list of the most recent posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public static IEnumerable<PostModel> GetRecentPosts(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            int page,
            int pageSize,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor)
        {
            var listNodes = GetListNodes(masterModel);

            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            var pager = new PagerModel(pageSize, page - 1, 1);

            var listItems = helper.GetPostsSortedByPublishedDate(pager, null, listNodeIds);

            var rootPageModel = new ListModel(listNodes[0], pager, listItems, publishedValueFallback, variationContextAccessor);
            return rootPageModel.Posts;
        }

        /// <summary>
        /// Returns a list of the most recent posts by archive
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public static IEnumerable<PostModel> GetRecentPostsByArchive(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            int page,
            int pageSize,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor)
        {
            var pager = new PagerModel(pageSize, page - 1, 1);

            var listItems = helper.GetPostsSortedByPublishedDate(pager, null, masterModel.Id);

            var rootPageModel = new ListModel(masterModel, pager, listItems, publishedValueFallback, variationContextAccessor);
            return rootPageModel.Posts;
        }

        internal static IEnumerable<IPublishedContent> GetContentByAuthor(
            this UmbracoHelper helper,
            IPublishedContent[] listNodes,
            string authorName,
            PagerModel pager,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor)
        {            
            var listNodeIds = listNodes.Select(x => x.Id).ToArray();           

            var postWithAuthor = helper.GetPostsSortedByPublishedDate(pager, x => string.Equals(x.Value<string>("author"), authorName.Replace("-", " "), StringComparison.InvariantCultureIgnoreCase), listNodeIds);

            var rootPageModel = new ListModel(listNodes[0], pager, postWithAuthor, publishedValueFallback, variationContextAccessor);
            return rootPageModel.Posts;
        }

        private static IPublishedContent[] GetListNodes(IMasterModel masterModel)
        {
            var listNodes = masterModel.RootBlogNode.ChildrenOfType(ArticulateConstants.ArticulateArchiveContentTypeAlias).ToArray();
            if (listNodes.Length == 0)
            {
                throw new InvalidOperationException(
                    "An ArticulateArchive document must exist under the root Articulate document");
            }

            return listNodes;
        }
        
        
        

    }
}
