#nullable enable
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;

namespace Articulate
{
    internal static class PagedChildrenExtensions
    {
        internal static IEnumerable<IContent> EnumeratePagedChildren(
            this IContentService contentService,
            int id,
            long pageIndex,
            int pageSize,
            out long totalRecords,
            IQuery<IContent>? filter = null,
            Ordering? ordering = null)
        {
            return contentService.GetPagedChildren(
                id,
                pageIndex,
                pageSize,
                out totalRecords,
                propertyAliases: null,
                filter,
                ordering,
                loadTemplates: true);
        }
    }
}
