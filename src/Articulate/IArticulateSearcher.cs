#nullable enable
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate
{
    /// <summary>
    ///     Provides searching capabilities for Articulate posts.
    /// </summary>
    public interface IArticulateSearcher
    {
        /// <summary>
        ///     Searches for posts based on the provided term and criteria.
        /// </summary>
        public IEnumerable<IPublishedContent>? Search(
            string term,
            string? indexName,
            int blogArchiveNodeId,
            int pageSize,
            int pageIndex,
            out long totalResults);
    }
}
