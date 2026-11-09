#nullable enable
using Articulate.Controllers.Api;
using Articulate.Swagger;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.OpenApi;

namespace Articulate.Swagger.V17
{
    /// <summary>
    ///     Handles the generation of operation IDs for Articulate API endpoints.
    /// </summary>
#pragma warning disable CS9107
    internal class ArticulateOperationIdHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
        : OperationIdHandler(apiVersioningOptions)
#pragma warning restore CS9107
    {
        /// <inheritdoc />
        public override string Handle(ApiDescription apiDescription) => ArticulateOperationId(apiDescription);

        /// <inheritdoc />
        protected override bool CanHandle(
            ApiDescription apiDescription,
            ControllerActionDescriptor controllerActionDescriptor)
        {
            // Handle only Articulate's own API controllers. The namespace is derived from a real
            // Articulate controller type rather than a literal so it can't silently diverge if the
            // controllers are moved.
            var namespaceName = typeof(BlogMlApiController).Namespace;
            var controllerNamespace = controllerActionDescriptor.ControllerTypeInfo.Namespace;

            return namespaceName is not null
                   && controllerNamespace?.StartsWith(
                       namespaceName,
                       StringComparison.InvariantCultureIgnoreCase) is true;
        }

        private string ArticulateOperationId(ApiDescription apiDescription)
        {
            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
            {
                throw new ArgumentException($"This handler operates only on {nameof(ControllerActionDescriptor)}.");
            }

            return ArticulateOperationIdGenerator.Generate(
                apiDescription,
                controllerActionDescriptor,
                apiVersioningOptions.Value.DefaultApiVersion);
        }
    }
}
