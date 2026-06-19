#nullable enable
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Web.Common;

namespace Articulate.Services
{
    /// <summary>
    ///     Repository interface for Articulate tag/category queries.
    /// </summary>
    public interface IArticulateTagRepository
    {
        /// <summary>
        ///     Gets all category names for a given blog root.
        /// </summary>
        /// <param name="masterModel">The blog root model.</param>
        /// <returns>A collection of category names.</returns>
        internal IEnumerable<string> GetAllCategories(IMasterModel masterModel);

        /// <summary>
        ///     Gets all distinct tag names for a given blog root path and tag group.
        /// </summary>
        /// <param name="rootPath">The root node path to scope the query to.</param>
        /// <param name="tagGroup">The Umbraco tag group.</param>
        /// <returns>A collection of distinct tag names.</returns>
        internal IEnumerable<string> GetAllTags(string rootPath, string tagGroup);

        /// <summary>
        ///     Gets all distinct tags with IDs for a given blog root path and tag group.
        /// </summary>
        /// <param name="rootPath">The root node path to scope the query to.</param>
        /// <param name="tagGroup">The Umbraco tag group.</param>
        /// <returns>A collection of distinct tag infos.</returns>
        internal IEnumerable<ArticulateTagInfo> GetAllTagInfos(string rootPath, string tagGroup);

        /// <summary>
        ///     Gets all posts grouped by tag for the tags listing page.
        /// </summary>
        /// <param name="helper">The Umbraco helper.</param>
        /// <param name="tagQuery">The tag query service.</param>
        /// <param name="masterModel">The blog root model.</param>
        /// <param name="tagGroup">The tag group.</param>
        /// <param name="baseUrlName">The base URL name for the tag route.</param>
        /// <returns>A collection of posts grouped by tag.</returns>
        public IEnumerable<PostsByTagModel> GetContentByTags(
            UmbracoHelper helper,
            ITagQuery tagQuery,
            IMasterModel masterModel,
            string tagGroup,
            string baseUrlName);

        /// <summary>
        ///     Gets paged posts for a specific tag.
        /// </summary>
        /// <param name="helper">The Umbraco helper.</param>
        /// <param name="masterModel">The blog root model.</param>
        /// <param name="tag">The tag name.</param>
        /// <param name="tagGroup">The tag group.</param>
        /// <param name="baseUrlName">The base URL name for the tag route.</param>
        /// <param name="page">The current page number.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>A model containing the posts for the specified tag.</returns>
        public PostsByTagModel GetContentByTag(
            UmbracoHelper helper,
            IMasterModel masterModel,
            string tag,
            string tagGroup,
            string baseUrlName,
            long page,
            long pageSize);
    }
}
