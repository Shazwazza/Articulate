#nullable enable
using System.Numerics;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;

namespace Articulate.Routing
{
    internal readonly struct ArticulateRouteTemplate(RouteTemplate routeTemplate) : IEquatable<ArticulateRouteTemplate>,
        IEqualityOperators<ArticulateRouteTemplate, ArticulateRouteTemplate, bool>
    {
        private readonly string _template = routeTemplate.TemplateText ?? string.Empty;

        public RouteTemplate RouteTemplate { get; } = routeTemplate;

        // TemplateMatcher is created once per route (at route-build time) instead of
        // per-route per-HTTP-request. TryMatch is thread-safe: it only writes to the
        // caller-supplied RouteValueDictionary, so this cached instance is safe to share.
        internal TemplateMatcher Matcher { get; } = new TemplateMatcher(routeTemplate, new RouteValueDictionary());

        public static bool operator ==(ArticulateRouteTemplate left, ArticulateRouteTemplate right) => left.Equals(right);

        public static bool operator !=(ArticulateRouteTemplate left, ArticulateRouteTemplate right) => !(left == right);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ArticulateRouteTemplate template && Equals(template);

        /// <inheritdoc/>
        public bool Equals(ArticulateRouteTemplate other) => _template == other._template;

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(_template);
    }
}
