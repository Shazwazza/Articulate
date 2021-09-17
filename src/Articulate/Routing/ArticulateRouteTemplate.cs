using Microsoft.AspNetCore.Routing.Template;
using System;

namespace Articulate.Routing
{
    internal struct ArticulateRouteTemplate : IEquatable<ArticulateRouteTemplate>
    {
        private readonly string _template;

        public ArticulateRouteTemplate(RouteTemplate routeTemplate)
        {
            RouteTemplate = routeTemplate;
            _template = routeTemplate.TemplateText;
        }
        
        public RouteTemplate RouteTemplate { get; }

        public override bool Equals(object obj) => obj is ArticulateRouteTemplate template && Equals(template);
        public bool Equals(ArticulateRouteTemplate other) => _template == other._template;
        public override int GetHashCode() => HashCode.Combine(_template);
    }
}
