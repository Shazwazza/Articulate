#nullable enable
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Routing
{
    internal static class ArticulateRouteChangeDetector
    {
        internal static bool AffectsArticulateRoutes(
            string? changedPath,
            int changedLevel,
            int changedSortOrder,
            string? changedContentTypeAlias,
            IEnumerable<IPublishedContent> articulateNodes)
        {
            if (changedContentTypeAlias.InvariantEquals(ArticulateConstants.ContentType.Articulate))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(changedPath))
            {
                return false;
            }

            var changedPathPrefix = changedPath + ",";
            foreach (IPublishedContent articulateNode in articulateNodes)
            {
                if (articulateNode.Path.InvariantStartsWith(changedPathPrefix))
                {
                    return true;
                }

                if (articulateNode.Level == changedLevel && articulateNode.SortOrder > changedSortOrder)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
