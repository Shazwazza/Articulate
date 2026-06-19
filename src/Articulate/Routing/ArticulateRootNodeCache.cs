#nullable enable
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Controllers;
using Umbraco.Cms.Core.Routing;

namespace Articulate.Routing
{
    /// <summary>
    ///     Used to create all the dynamic routes.
    /// </summary>
    internal class ArticulateRootNodeCache(ControllerActionDescriptor controllerActionDescriptor)
    {
        private readonly ConcurrentDictionary<int, IReadOnlyList<Domain>> _content = new();

        public ControllerActionDescriptor ControllerActionDescriptor { get; } = controllerActionDescriptor;

        public void Add(int contentId, IReadOnlyList<Domain> domains)
            => _content.TryAdd(contentId, domains);

        public int GetContentId(Domain? currentDomain)
        {
            KeyValuePair<int, IReadOnlyList<Domain>> found = _content.FirstOrDefault(x =>
                (currentDomain is null && x.Value.Count == 0) ||
                (currentDomain is not null && x.Value.Any(d =>
                    ArticulateDomainMatcher.Matches(d, currentDomain, (currentDomain as DomainAndUri)?.Uri))));

            return found.Key; // 0 if no match (default KeyValuePair<int,...>.Key)
        }
    }
}
