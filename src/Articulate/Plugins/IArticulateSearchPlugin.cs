using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Articulate.Plugins
{
    public interface IArticulateSearchPlugin
    {
        IEnumerable<IPublishedContent> Search(string term, string provider, int blogArchiveNodeId);
    }
}
