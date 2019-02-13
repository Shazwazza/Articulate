using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate
{
    public interface IArticulateSearcher
    {
        IEnumerable<IPublishedContent> Search(string term, string searcherName, int blogArchiveNodeId, int pageSize, int pageIndex, out long totalResults);
    }
}
