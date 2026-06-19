#nullable enable
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common;

namespace Articulate.Services
{
    /// <summary>
    ///     Service layer for Articulate tag/category queries.
    /// </summary>
    /// <remarks>
    ///     Provides database scoping for <see cref="ArticulateTagRepository" />, which uses custom SQL
    ///     for path-scoped queries (multi-blog), paging, and sorting by publishedDate.
    /// </remarks>
    public class ArticulateTagService(
        IArticulateTagRepository repository,
        ICoreScopeProvider provider,
        ILoggerFactory loggerFactory,
        IEventMessagesFactory eventMessagesFactory)
        : RepositoryService(provider, loggerFactory, eventMessagesFactory)
    {
        /// <summary>
        ///     Gets content grouped by tags.
        /// </summary>
        /// <param name="helper">The Umbraco helper.</param>
        /// <param name="tagQuery">The tag query service.</param>
        /// <param name="masterModel">The master model for the blog root.</param>
        /// <param name="tagGroup">The tag group to filter by.</param>
        /// <param name="baseUrlName">The base URL name for the tag route.</param>
        /// <returns>A collection of posts grouped by tag.</returns>
        public IEnumerable<PostsByTagModel> GetContentByTags(
            UmbracoHelper helper,
            ITagQuery tagQuery,
            IMasterModel masterModel,
            string tagGroup,
            string baseUrlName)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return repository.GetContentByTags(
                    helper,
                    tagQuery,
                    masterModel,
                    tagGroup,
                    baseUrlName);
            }
        }

        /// <summary>
        ///     Gets content for a specific tag with paging.
        /// </summary>
        /// <param name="helper">The Umbraco helper.</param>
        /// <param name="masterModel">The master model for the blog root.</param>
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
            long pageSize)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return repository.GetContentByTag(
                    helper,
                    masterModel,
                    tag,
                    tagGroup,
                    baseUrlName,
                    page,
                    pageSize);
            }
        }

        // Not used internally or by default themes, but exposed for custom themes
        /// <summary>
        ///     Gets all categories for a given blog root.
        /// </summary>
        /// <param name="masterModel">The master model for the blog root.</param>
        /// <returns>A collection of category names.</returns>
        public IEnumerable<string> GetAllCategories(
            IMasterModel masterModel)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return repository.GetAllCategories(masterModel);
            }
        }

        /// <summary>
        ///     Gets all distinct tags or categories for a given blog root path.
        /// </summary>
        /// <param name="rootPath">The blog root path.</param>
        /// <param name="tagGroup">The Umbraco tag group.</param>
        /// <returns>A collection of distinct tag names.</returns>
        internal IEnumerable<string> GetAllTags(string rootPath, string tagGroup)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return repository.GetAllTags(rootPath, tagGroup);
            }
        }

        /// <summary>
        ///     Gets all distinct tags with IDs for a given blog root path.
        /// </summary>
        /// <param name="rootPath">The blog root path.</param>
        /// <param name="tagGroup">The Umbraco tag group.</param>
        /// <returns>A collection of tag infos.</returns>
        internal IEnumerable<ArticulateTagInfo> GetAllTagInfos(string rootPath, string tagGroup)
        {
            using (ScopeProvider.CreateCoreScope(autoComplete: true))
            {
                return repository.GetAllTagInfos(rootPath, tagGroup);
            }
        }
    }
}
