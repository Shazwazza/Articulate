#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Umbraco.Cms.Api.Common.Security;

namespace Articulate.Swagger.V18
{
    /// <summary>
    /// Adds security requirements to Articulate API operations for OpenAPI documentation in Umbraco 18+.
    /// </summary>
    internal class ArticulateOperationSecurityFilter : IOpenApiOperationTransformer, IOpenApiDocumentTransformer
    {
        private const string BackOfficeUserSecurityName = "Backoffice-User";

        /// <inheritdoc/>
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            if (context.Description.ActionDescriptor is not ControllerActionDescriptor description)
            {
                return Task.CompletedTask;
            }
            if (description.MethodInfo.GetCustomAttributes(true).Any(x => x is AllowAnonymousAttribute) ||
                description.MethodInfo.DeclaringType?.GetCustomAttributes(true).Any(x => x is AllowAnonymousAttribute) == true)
            {
                operation.Security = [];
                return Task.CompletedTask;
            }
            var schemaRef = new OpenApiSecuritySchemeReference(BackOfficeUserSecurityName, context.Document);
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement { [schemaRef] = [] });
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            var apiKeyScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Name = "Umbraco",
                In = ParameterLocation.Header,
                Description = "Umbraco Authentication",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(Paths.BackOfficeApi.AuthorizationEndpoint, UriKind.Relative),
                        TokenUrl = new Uri(Paths.BackOfficeApi.TokenEndpoint, UriKind.Relative),
                    },
                },
            };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BackOfficeUserSecurityName] = apiKeyScheme;
            var schemaRef = new OpenApiSecuritySchemeReference(BackOfficeUserSecurityName, document);
            document.Security ??= new List<OpenApiSecurityRequirement>();
            document.Security.Add(new OpenApiSecurityRequirement { [schemaRef] = [] });
            return Task.CompletedTask;
        }
    }
}
