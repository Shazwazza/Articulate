using System.Collections.Generic;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate
{
    public interface IArticulateSearcher
    {
        IEnumerable<IPublishedContent> Search(string term, string indexName, int blogArchiveNodeId, int pageSize, int pageIndex, out long totalResults);
    }
}
