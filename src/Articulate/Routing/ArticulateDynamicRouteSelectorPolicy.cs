using Articulate.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Umbraco.Cms.Web.Common.Routing;

#nullable enable
namespace Articulate.Routing
{
    /// <summary>
    /// Resolves ambiguity between Umbraco's and Articulate's dynamic routes.
    /// </summary>
    /// <remarks>
    /// When a 404 page is configured, Umbraco's earlier, lower-order dynamic route still yields a candidate for
    /// Articulate paths and would win. If Articulate routed the request, only Articulate's candidates are kept.
    /// </remarks>
    internal class ArticulateDynamicRouteSelectorPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        /// <inheritdoc/>
        public override int Order => 100;

        /// <inheritdoc/>
        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints) =>
            // Dynamic route placeholders carry no controller metadata; ApplyAsync checks for Articulate per request.
            endpoints.All(x => x.Metadata.GetMetadata<IDynamicEndpointMetadata>() is not null);

        /// <inheritdoc/>
        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            Type? controller = httpContext.Features.Get<UmbracoRouteValues>()?.ControllerActionDescriptor.ControllerTypeInfo;

            // Only intervene when Articulate's transformer routed to an Articulate controller.
            if (controller is null || !controller.IsDefined(typeof(ArticulateDynamicRouteAttribute), inherit: true))
            {
                return Task.CompletedTask;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                Endpoint endpoint = candidates[i].Endpoint;
                candidates.SetValidity(
                    i,
                    endpoint.Metadata.GetMetadata<ArticulateDynamicRouteAttribute>() is not null);
            }

            return Task.CompletedTask;
        }
    }
}
