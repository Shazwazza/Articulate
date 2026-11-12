#nullable enable
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate
{
    /// <summary>
    /// Provides searching capabilities for Articulate posts.
    /// </summary>
    public interface IArticulateSearcher
    {
#if UMBRACO_19_OR_GREATER
        /// <summary>
        /// Searches the published content index for posts based on the provided term and criteria.
        /// </summary>
        public Task<(IEnumerable<IPublishedContent> Results, long TotalResults)> SearchAsync(
            string term,
            int blogArchiveNodeId,
            int pageSize,
            int pageIndex);
#else
        /// <summary>
        /// Searches for posts based on the provided term and criteria.
        /// </summary>
        public IEnumerable<IPublishedContent>? Search(
            string term,
            string? indexName,
            int blogArchiveNodeId,
            int pageSize,
            int pageIndex,
            out long totalResults);
#endif
    }
}
