#nullable enable
using Articulate.Controllers.Api;
using Articulate.Swagger;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Articulate.Swagger.V18
{
    /// <summary>
    /// Transforms OpenAPI operation IDs for Articulate API endpoints in Umbraco 18+.
    /// </summary>
    internal class ArticulateOperationIdHandler : IOpenApiOperationTransformer
    {
        /// <inheritdoc/>
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            var operationId = GenerateOperationId(context);
            if (operationId is not null)
            {
                operation.OperationId = operationId;
            }

            return Task.CompletedTask;
        }

        private static string? GenerateOperationId(OpenApiOperationTransformerContext context)
        {
            ApiDescription apiDescription = context.Description;
            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
            {
                return null;
            }

            // Handle only Articulate's own API controllers. The namespace is derived from a real
            // Articulate controller type rather than a literal so it can't silently diverge if the
            // controllers are moved.
            var namespaceName = typeof(BlogMlApiController).Namespace;
            var controllerNamespace = controllerActionDescriptor.ControllerTypeInfo.Namespace;

            var shouldHandle = namespaceName is not null
                               && controllerNamespace?.StartsWith(namespaceName, StringComparison.InvariantCultureIgnoreCase) is true;

            if (!shouldHandle)
            {
                return null;
            }

            var httpMethod = apiDescription.HttpMethod?.ToLower().ToFirstUpper() ?? "Get";

            // Respect an explicit route Name when one is set; the OpenAPI framework uses this as the
            // operation ID directly (with the HTTP method prepended if not already there).
            var explicitName = apiDescription.ActionDescriptor.AttributeRouteInfo?.Name;
            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                return explicitName.InvariantStartsWith(httpMethod)
                    ? explicitName
                    : $"{httpMethod}{explicitName}";
            }

            return ArticulateOperationIdGenerator.Generate(
                apiDescription,
                controllerActionDescriptor,
                context.ApplicationServices.GetRequiredService<IOptions<ApiVersioningOptions>>().Value.DefaultApiVersion);
        }
    }
}
