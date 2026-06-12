#nullable enable
#if !(NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER)
using Asp.Versioning;
using Articulate.Controllers.Api;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.OpenApi;

namespace Articulate.Swagger
{
    /// <summary>
    /// Handles the generation of operation IDs for Articulate API endpoints.
    /// </summary>
#pragma warning disable CS9107
    internal class ArticulateOperationIdHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
        : OperationIdHandler(apiVersioningOptions)
#pragma warning restore CS9107
    {
        /// <inheritdoc/>
        public override string Handle(ApiDescription apiDescription) => ArticulateOperationId(apiDescription);

        /// <inheritdoc/>
        protected override bool CanHandle(
            ApiDescription apiDescription,
            ControllerActionDescriptor controllerActionDescriptor)
        {
            Type type = typeof(BlogMlApiController);
            var namespaceName = type.Namespace ?? "Articulate.Api.Management.Controllers";
            var controllerNamespace = controllerActionDescriptor.ControllerTypeInfo.Namespace;

            return controllerNamespace?.StartsWith(
                       namespaceName,
                       StringComparison.InvariantCultureIgnoreCase) is true
                   || controllerNamespace?.StartsWith(
                       "Articulate.Api.Management.Controllers",
                       StringComparison.InvariantCultureIgnoreCase) is true;
        }

        private string ArticulateOperationId(ApiDescription apiDescription)
        {
            if (apiDescription.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
            {
                throw new ArgumentException($"This handler operates only on {nameof(ControllerActionDescriptor)}.");
            }

            ApiVersion defaultVersion = apiVersioningOptions.Value.DefaultApiVersion;
            var httpMethod = apiDescription.HttpMethod?.ToLower().ToFirstUpper() ?? "Get";
            var relativePath = apiDescription.RelativePath;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException(
                    $"There is no relative path for controller action {apiDescription.ActionDescriptor.RouteValues["controller"]}");
            }

            var unprefixedRelativePath = ArticulateOperationIdRegexes
                .VersionPrefixRegex()
                .Replace(relativePath, string.Empty);

            var formattedOperationId = ArticulateOperationIdRegexes
                .TemplatePlaceholdersRegex()
                .Replace(unprefixedRelativePath, m => $"By{m.Groups[1].Value.ToFirstUpper()}");

            formattedOperationId = ArticulateOperationIdRegexes
                .ToCamelCaseRegex()
                .Replace(formattedOperationId, m => m.Groups[1].Value.ToUpper());

            var version = string.Empty;
            var versionAttributeValue = controllerActionDescriptor.MethodInfo.GetMapToApiVersionAttributeValue();

            if (!string.IsNullOrEmpty(versionAttributeValue) &&
                !string.Equals(versionAttributeValue, defaultVersion.ToString(), StringComparison.Ordinal))
            {
                version = versionAttributeValue;
            }

            return $"{httpMethod}{formattedOperationId.ToFirstUpper()}{version}";
        }
    }
}
#else
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Articulate.Swagger
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

            Type type = typeof(Controllers.Api.BlogMlApiController);
            var namespaceName = type.Namespace ?? "Articulate.Api.Management.Controllers";
            var controllerNamespace = controllerActionDescriptor.ControllerTypeInfo.Namespace;

            var shouldHandle = controllerNamespace?.StartsWith(namespaceName, StringComparison.InvariantCultureIgnoreCase) is true
                               || controllerNamespace?.StartsWith("Articulate.Api.Management.Controllers", StringComparison.InvariantCultureIgnoreCase) is true;

            if (!shouldHandle)
            {
                return null;
            }

            ApiVersion defaultVersion = context.ApplicationServices.GetRequiredService<IOptions<ApiVersioningOptions>>().Value.DefaultApiVersion;
            var httpMethod = apiDescription.HttpMethod?.ToLower().ToFirstUpper() ?? "Get";

            if (string.IsNullOrWhiteSpace(apiDescription.ActionDescriptor.AttributeRouteInfo?.Name) == false)
            {
                var explicitOperationId = apiDescription.ActionDescriptor.AttributeRouteInfo!.Name;
                return explicitOperationId.InvariantStartsWith(httpMethod)
                    ? explicitOperationId
                    : $"{httpMethod}{explicitOperationId}";
            }

            var relativePath = apiDescription.RelativePath;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException(
                    $"There is no relative path for controller action {apiDescription.ActionDescriptor.RouteValues["controller"]}");
            }

            var unprefixedRelativePath = ArticulateOperationIdRegexes
                .VersionPrefixRegex()
                .Replace(relativePath, string.Empty);

            var formattedOperationId = ArticulateOperationIdRegexes
                .TemplatePlaceholdersRegex()
                .Replace(unprefixedRelativePath, m => $"By{m.Groups[1].Value.ToFirstUpper()}");

            formattedOperationId = ArticulateOperationIdRegexes
                .ToCamelCaseRegex()
                .Replace(formattedOperationId, m => m.Groups[1].Value.ToUpper());

            string? version = null;
            var versionAttributeValue = controllerActionDescriptor.MethodInfo.GetMapToApiVersionAttributeValue();

            if (string.Equals(versionAttributeValue, defaultVersion.ToString(), StringComparison.Ordinal) == false)
            {
                version = versionAttributeValue;
            }

            return $"{httpMethod}{formattedOperationId.ToFirstUpper()}{version}";
        }
    }
}
#endif
