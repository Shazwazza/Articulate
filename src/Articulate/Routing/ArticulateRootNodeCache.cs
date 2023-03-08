using Microsoft.AspNetCore.Mvc.Controllers;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Routing;

namespace Articulate.Routing
{
    /// <summary>
    /// Used to create all of the dynamic routes.
    /// </summary>
    public class ArticulateRootNodeCache
    {
        private readonly Dictionary<int, IReadOnlyList<Domain>> _content = new();

        public ArticulateRootNodeCache(ControllerActionDescriptor controllerActionDescriptor)
        {
            ControllerActionDescriptor = controllerActionDescriptor;
        }

        public ControllerActionDescriptor ControllerActionDescriptor { get; }

        public void Add(int contentId, IReadOnlyList<Domain> domains)
            => _content.Add(contentId, domains);

        public int GetContentId(Domain currentDomain)
        {
            var found = _content.First(x =>
                (currentDomain == null && x.Value.Count == 0) || x.Value.Any(x => x.Id == currentDomain?.Id));

            return found.Key;
        }
    }
}
