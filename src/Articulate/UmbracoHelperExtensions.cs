#nullable enable
using Articulate.Services;
using Articulate.Routing;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Web.Common;
using PublishedContentExtensions = Articulate.Models.PublishedContentExtensions;

namespace Articulate
{
    /// <summary>
    /// Extension methods for <see cref="UmbracoHelper"/>.
    /// </summary>
    public static class UmbracoHelperExtensions
    {
        /// <summary>
        /// Gets a paged set of posts sorted by published date, along with the total count.
        /// </summary>
        internal static (int TotalPosts, IPublishedContent[] Posts) GetPagedPostsSortedByPublishedDate(
            this UmbracoHelper helper,
            PagerModel pager,
            Func<IPublishedContent, bool>? filter,
            params int[] articulateArchiveIds)
        {
            IEnumerable<IPublishedContent> posts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants());

            // apply a filter if there is one
            if (filter is not null)
            {
                posts = posts.Where(filter);
            }

            (IPublishedContent Content, DateTime PublishedDate)[] orderedPosts = posts
                .Select(x => (Content: x, PublishedDate: x.Value<DateTime>("publishedDate")))
                .OrderByDescending(x => x.PublishedDate)
                .ToArray();

            IPublishedContent[] pagedPosts = orderedPosts
                .Skip(pager.CurrentPageIndex * pager.PageSize)
                .Take(pager.PageSize)
                .Select(x => x.Content)
                .ToArray();

            return (orderedPosts.Length, pagedPosts);
        }

        /// <summary>
        /// Gets posts sorted by published date.
        /// </summary>
        public static IEnumerable<IPublishedContent> GetPostsSortedByPublishedDate(
            this UmbracoHelper helper,
            PagerModel pager,
            Func<IPublishedContent, bool>? filter,
            params int[] articulateArchiveIds)
            => helper.GetPagedPostsSortedByPublishedDate(pager, filter, articulateArchiveIds).Posts;

        /// <summary>
        /// Gets a collection of tags for the blog.
        /// </summary>

        // Not used internally or by default themes, but exposed for custom themes
        public static PostTagCollection GetPostTagCollection(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            ITagQuery tagQuery,
            ArticulateTagService articulateTagService)
        {
            string? tagsBaseUrl = ArticulateRouteSegmentHelper.GetConfiguredSegment(masterModel.RootBlogNode, "tagsUrlName");
            if (tagsBaseUrl is null)
            {
                return new PostTagCollection([]);
            }

            IEnumerable<PostsByTagModel> contentByTags = articulateTagService.GetContentByTags(
                helper,
                tagQuery,
                masterModel,
                ArticulateConstants.DataType.ArticulateTags,
                tagsBaseUrl);

            return new PostTagCollection(contentByTags);
        }

        /// <summary>
        /// Gets a list of the most recent posts.
        /// </summary>

        // Not used internally or by default themes, but exposed for custom themes
        public static IEnumerable<PostModel> GetRecentPosts(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            int count,
            IPublishedValueFallback publishedValueFallback)
        {
            IPublishedContent[] listNodes = PublishedContentExtensions.GetListNodes(masterModel);

            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            var pager = new PagerModel(count, 0, 1);

            IPublishedContent[] listItems = helper.GetPagedPostsSortedByPublishedDate(pager, null, listNodeIds).Posts;

            var rootPageModel = new ListModel(listNodes[0], pager, listItems, publishedValueFallback, masterModel.CommentsOptions);
            return rootPageModel.Posts;
        }

        /// <summary>
        /// Returns a list of the most recent posts
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <param name="publishedValueFallback"></param>
        /// <returns></returns>

        // Not used internally or by default themes, but exposed for custom themes
        public static IEnumerable<PostModel> GetRecentPosts(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            int page,
            int pageSize,
            IPublishedValueFallback publishedValueFallback)
        {
            IPublishedContent[] listNodes = PublishedContentExtensions.GetListNodes(masterModel);

            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            var pager = new PagerModel(pageSize, page - 1, 1);

            IPublishedContent[] listItems = helper.GetPagedPostsSortedByPublishedDate(pager, null, listNodeIds).Posts;

            var rootPageModel = new ListModel(listNodes[0], pager, listItems, publishedValueFallback, masterModel.CommentsOptions);
            return rootPageModel.Posts;
        }

        /// <summary>
        /// Returns a list of the most recent posts by archive
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="masterModel"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <param name="publishedValueFallback"></param>
        /// <returns></returns>

        // Not used internally or by default themes, but exposed for custom themes

        public static IEnumerable<PostModel> GetRecentPostsByArchive(
            this UmbracoHelper helper,
            IMasterModel masterModel,
            int page,
            int pageSize,
            IPublishedValueFallback publishedValueFallback)
        {
            var pager = new PagerModel(pageSize, page - 1, 1);

            IPublishedContent[] listItems = helper.GetPagedPostsSortedByPublishedDate(pager, null, masterModel.Id).Posts;

            var rootPageModel = new ListModel(masterModel, pager, listItems, publishedValueFallback, masterModel.CommentsOptions);
            return rootPageModel.Posts;
        }

        internal static (int TotalPosts, IPublishedContent[] Posts) GetPagedContentByAuthor(
            this UmbracoHelper helper,
            IPublishedContent[] listNodes,
            string authorName,
            PagerModel pager)
        {
            int[] listNodeIds = listNodes.Select(x => x.Id).ToArray();

            (int TotalPosts, IPublishedContent[] Posts) postWithAuthor = helper.GetPagedPostsSortedByPublishedDate(
                pager,
                x => string.Equals(
                    x.Value<string>("author"),
                    NormalizeAuthorName(authorName),
                    StringComparison.InvariantCultureIgnoreCase),
                listNodeIds);

            return postWithAuthor;
        }

        internal static IReadOnlyDictionary<string, (int PostCount, DateTime? LastPostDate)> GetAuthorPostStatsByAuthor(
            this UmbracoHelper helper,
            params int[] articulateArchiveIds)
        {
            IEnumerable<IPublishedContent> posts = articulateArchiveIds
                .Select(helper.Content)
                .WhereNotNull()
                .SelectMany(x => x.Descendants());

            return posts
                .Select(x => new
                {
                    AuthorName = NormalizeAuthorName(x.Value<string>("author")),
                    PublishedDate = x.Value<DateTime>("publishedDate")
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.AuthorName))
                .GroupBy(x => x.AuthorName, StringComparer.InvariantCultureIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => ((int PostCount, DateTime? LastPostDate))(x.Count(), x.Max(p => (DateTime?)p.PublishedDate)),
                    StringComparer.InvariantCultureIgnoreCase);
        }

        internal static string NormalizeAuthorName(string? authorName) =>
            (authorName ?? string.Empty)
                .Trim();
    }
}
