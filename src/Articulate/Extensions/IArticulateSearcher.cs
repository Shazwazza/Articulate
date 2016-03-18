using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Articulate.Extensions
{
    public interface IArticulateSearcher
    {
        IEnumerable<IPublishedContent> Search(string term, string provider, int blogArchiveNodeId);
    }
}
