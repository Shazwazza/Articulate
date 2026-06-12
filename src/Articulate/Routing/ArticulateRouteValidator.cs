#nullable enable
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Strings;
#if UMBRACO_18_OR_GREATER
using Umbraco.Cms.Core.Services;
#endif

namespace Articulate.Routing
{
    internal static class ArticulateRouteValidator
    {
        private static readonly string[] _configuredRouteAliases = ["searchUrlName", "categoriesUrlName", "tagsUrlName"];

        private static readonly HashSet<string> _reservedRouteSegments = new(StringComparer.OrdinalIgnoreCase)
        {
            "a-new",
            "author",
            "metaweblog",
            "opensearch",
            "rss",
            "rsd",
            "wlwmanifest"
        };

        internal static List<Domain> DomainsForContent(IPublishedContent content, IReadOnlyList<Domain> domains)
        {
            HashSet<int> nodePaths = GetNodePaths(content.Path, content.Name, content.Id);
            return [.. domains.Where(domain => nodePaths.Contains(domain.ContentId))];
        }

        internal static List<Domain> DomainsForContent(IContent content, IReadOnlyList<Domain> domains)
        {
            HashSet<int> nodePaths = GetNodePaths(content.Path, content.Name ?? content.Id.ToString(), content.Id);
            return [.. domains.Where(domain => nodePaths.Contains(domain.ContentId))];
        }

        internal static void ValidateConfiguredRouteSegments(
            IPublishedContent articulateRootNode,
#if UMBRACO_18_OR_GREATER
            IDocumentUrlService documentUrlService,
#endif
            IEnumerable<IPublishedContent>? children = null)
        {
            var configuredSegments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<IPublishedContent> configuredChildren = children ?? articulateRootNode.Children();
            foreach (IPublishedContent child in configuredChildren)
            {
#if UMBRACO_18_OR_GREATER
                string? childRouteSegment = ArticulateRouteSegmentHelper.NormalizeOrNull(documentUrlService.GetUrlSegment(child.Key, string.Empty, true));
#else
                string? childRouteSegment = ArticulateRouteSegmentHelper.NormalizeOrNull(child.UrlSegment);
#endif
                if (childRouteSegment is not null)
                {
                    string childDescription = $"child content '{child.Name}'";
                    if (configuredSegments.TryGetValue(childRouteSegment, out string? existingRouteSource))
                    {
                        throw new InvalidOperationException(
                            $"Articulate root '{articulateRootNode.Name}' (id: {articulateRootNode.Id}) uses the same route segment " +
                            $"'{childRouteSegment}' for both '{existingRouteSource}' and '{childDescription}'.");
                    }

                    configuredSegments[childRouteSegment] = childDescription;
                }
            }

            foreach (string propertyAlias in _configuredRouteAliases)
            {
                ValidateConfiguredRouteSegment(
                    articulateRootNode,
                    propertyAlias,
                    articulateRootNode.Value<string>(propertyAlias),
                    configuredSegments);
            }
        }

        internal static void ValidateConfiguredRouteSegments(
            IContent articulateRootNode,
            IReadOnlyList<IContent> children,
            IShortStringHelper shortStringHelper,
            IEnumerable<IUrlSegmentProvider> urlSegmentProviders)
        {
            IReadOnlyList<IUrlSegmentProvider> urlSegmentProviderList = urlSegmentProviders as IReadOnlyList<IUrlSegmentProvider>
                ?? urlSegmentProviders.ToList();
            var configuredSegments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (IContent child in children)
            {
                string? childRouteSegment = ArticulateRouteSegmentHelper.NormalizeOrNull(
                    child.GetUrlSegment(shortStringHelper, urlSegmentProviderList, culture: null, published: false));

                if (childRouteSegment is null)
                {
                    continue;
                }

                string childDescription = $"child content '{child.Name}'";
                if (configuredSegments.TryGetValue(childRouteSegment, out string? existingRouteSource))
                {
                    throw new InvalidOperationException(
                        $"Articulate root '{articulateRootNode.Name}' (id: {articulateRootNode.Id}) uses the same route segment " +
                        $"'{childRouteSegment}' for both '{existingRouteSource}' and '{childDescription}'.");
                }

                configuredSegments[childRouteSegment] = childDescription;
            }

            foreach (string propertyAlias in _configuredRouteAliases)
            {
                ValidateConfiguredRouteSegment(
                    articulateRootNode.Name ?? articulateRootNode.Id.ToString(),
                    articulateRootNode.Id,
                    propertyAlias,
                    articulateRootNode.GetValue<string>(propertyAlias),
                    configuredSegments);
            }
        }

        internal static void ValidateRootPathMappings(
            string rootNodePath,
            IReadOnlyList<IPublishedContent> articulateRoots,
            IReadOnlyList<Domain> domains,
            Uri currentUri)
        {
            var rootsWithDomains = articulateRoots
                .Select(root => (Root: root, Domains: DomainsForContent(root, domains)))
                .ToList();

            for (var i = 0; i < rootsWithDomains.Count; i++)
            {
                for (var j = i + 1; j < rootsWithDomains.Count; j++)
                {
                    (IPublishedContent leftRoot, List<Domain> leftDomains) = rootsWithDomains[i];
                    (IPublishedContent rightRoot, List<Domain> rightDomains) = rootsWithDomains[j];

                    List<string> overlappingDomains =
                    [
                        .. leftDomains
                            .Where(leftDomain => rightDomains.Any(rightDomain => ArticulateDomainMatcher.Matches(leftDomain, rightDomain, currentUri)))
                            .Select(leftDomain => leftDomain.Name)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                    ];

                    if ((leftDomains.Count != 0 || rightDomains.Count != 0) && overlappingDomains.Count <= 0)
                    {
                        continue;
                    }

                    var overlapDescription = overlappingDomains.Count > 0
                        ? $"overlapping domains: {string.Join(", ", overlappingDomains)}"
                        : "no domain assignments";

                    throw new InvalidOperationException(
                        $"Ambiguous Articulate root routing detected for path '{rootNodePath}' between " +
                        $"'{DescribeRoot(leftRoot)}' and '{DescribeRoot(rightRoot)}' ({overlapDescription}). " +
                        "Use distinct paths or non-overlapping domain assignments.");
                }
            }
        }

        internal static void ValidateRootPathMappings(
            string rootNodePath,
            IReadOnlyList<IContent> articulateRoots,
            IReadOnlyList<Domain> domains,
            Uri currentUri)
        {
            var rootsWithDomains = articulateRoots
                .Select(root => (Root: root, Domains: DomainsForContent(root, domains)))
                .ToList();

            for (var i = 0; i < rootsWithDomains.Count; i++)
            {
                for (var j = i + 1; j < rootsWithDomains.Count; j++)
                {
                    (IContent leftRoot, List<Domain> leftDomains) = rootsWithDomains[i];
                    (IContent rightRoot, List<Domain> rightDomains) = rootsWithDomains[j];

                    List<string> overlappingDomains =
                    [
                        .. leftDomains
                            .Where(leftDomain => rightDomains.Any(rightDomain => ArticulateDomainMatcher.Matches(leftDomain, rightDomain, currentUri)))
                            .Select(leftDomain => leftDomain.Name)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                    ];

                    if ((leftDomains.Count != 0 || rightDomains.Count != 0) && overlappingDomains.Count <= 0)
                    {
                        continue;
                    }

                    var overlapDescription = overlappingDomains.Count > 0
                        ? $"overlapping domains: {string.Join(", ", overlappingDomains)}"
                        : "no domain assignments";

                    throw new InvalidOperationException(
                        $"Ambiguous Articulate root routing detected for path '{rootNodePath}' between " +
                        $"'{DescribeRoot(leftRoot)}' and '{DescribeRoot(rightRoot)}' ({overlapDescription}). " +
                        "Use distinct paths or non-overlapping domain assignments.");
                }
            }
        }

        internal static void ValidateConfiguredRouteSegment(
            IPublishedContent articulateRootNode,
            string propertyAlias,
            string? routeSegment,
            IDictionary<string, string> configuredSegments) =>
            ValidateConfiguredRouteSegment(
                articulateRootNode.Name,
                articulateRootNode.Id,
                propertyAlias,
                routeSegment,
                configuredSegments);

        private static void ValidateConfiguredRouteSegment(
            string articulateRootName,
            int articulateRootId,
            string propertyAlias,
            string? routeSegment,
            IDictionary<string, string> configuredSegments)
        {
            string? normalizedRouteSegment = ArticulateRouteSegmentHelper.NormalizeOrNull(routeSegment);
            if (normalizedRouteSegment is null)
            {
                if (!string.IsNullOrEmpty(routeSegment))
                {
                    throw new InvalidOperationException(
                        $"Articulate root '{articulateRootName}' (id: {articulateRootId}) has an invalid value '{routeSegment}' " +
                        $"for '{propertyAlias}'. Route segments must contain at least one non-slash character.");
                }

                return;
            }

            if (normalizedRouteSegment.Any(char.IsWhiteSpace) ||
                normalizedRouteSegment.IndexOfAny(['/', '\\', '{', '}', '?', '#']) >= 0)
            {
                throw new InvalidOperationException(
                    $"Articulate root '{articulateRootName}' (id: {articulateRootId}) has an invalid value '{routeSegment}' " +
                    $"for '{propertyAlias}'. Route segments must be a single URL segment.");
            }

            if (_reservedRouteSegments.Contains(normalizedRouteSegment))
            {
                throw new InvalidOperationException(
                    $"Articulate root '{articulateRootName}' (id: {articulateRootId}) cannot use reserved route segment " +
                    $"'{normalizedRouteSegment}' for '{propertyAlias}'.");
            }

            if (configuredSegments.TryGetValue(normalizedRouteSegment, out string? existingPropertyAlias))
            {
                throw new InvalidOperationException(
                    $"Articulate root '{articulateRootName}' (id: {articulateRootId}) uses the same route segment " +
                    $"'{normalizedRouteSegment}' for both '{existingPropertyAlias}' and '{propertyAlias}'.");
            }

            configuredSegments[normalizedRouteSegment] = propertyAlias;
        }

        private static string DescribeRoot(IPublishedContent articulateRootNode) =>
            $"{articulateRootNode.Name} (id: {articulateRootNode.Id})";

        private static string DescribeRoot(IContent articulateRootNode) =>
            $"{articulateRootNode.Name} (id: {articulateRootNode.Id})";

        private static HashSet<int> GetNodePaths(string path, string contentName, int contentId)
        {
            HashSet<int> nodePaths = [];

            foreach (string pathSegment in path.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(pathSegment, out int pathId))
                {
                    throw new InvalidOperationException(
                        $"Articulate root '{contentName}' (id: {contentId}) has invalid path '{path}'.");
                }

                nodePaths.Add(pathId);
            }

            return nodePaths;
        }
    }
}
