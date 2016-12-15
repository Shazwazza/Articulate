using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Articulate
{
    public interface IArticulateSearcher
    {
        IEnumerable<IPublishedContent> Search(string term, string provider, int blogArchiveNodeId, int pageSize, int pageIndex, out int totalResults);
    }
}
