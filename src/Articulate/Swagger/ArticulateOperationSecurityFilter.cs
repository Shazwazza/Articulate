#nullable enable
#if !(NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER)
using Umbraco.Cms.Api.Management.OpenApi;

namespace Articulate.Swagger
{
    /// <summary>
    /// Adds backoffice security requirements to Articulate API operations.
    /// </summary>
    internal class ArticulateOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        /// <summary>
        /// Gets the API name for which security requirements are applied.
        /// </summary>
        protected override string ApiName => ArticulateConstants.ManagementApi.Name;
    }
}
#else
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Umbraco.Cms.Api.Common.Security;

namespace Articulate.Swagger
{
    /// <summary>
    /// Adds backoffice security requirements for Articulate API operations in Umbraco 18+ OpenAPI.
    /// </summary>
    internal class ArticulateOperationSecurityFilter : IOpenApiOperationTransformer, IOpenApiDocumentTransformer
    {
        private const int BaseAuthorizeAttributeCount = 2;
        private const string BackOfficeUserSecurityName = "Backoffice-User";

        /// <inheritdoc />
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

            operation.Responses ??= new OpenApiResponses();
            operation.Responses[StatusCodes.Status401Unauthorized.ToString()] = new OpenApiResponse
            {
                Description = "The resource is protected and requires an authentication token"
            };

            var schemaRef = new OpenApiSecuritySchemeReference(BackOfficeUserSecurityName, context.Document);
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement { [schemaRef] = [] });

            var numberOfAuthorizeAttributes =
                description.MethodInfo.GetCustomAttributes(true).Count(x => x is AuthorizeAttribute)
                + description.MethodInfo.DeclaringType?.GetCustomAttributes(true).Count(x => x is AuthorizeAttribute);

            if (numberOfAuthorizeAttributes > BaseAuthorizeAttributeCount || InjectsAuthorizationService(description.MethodInfo.DeclaringType))
            {
                operation.Responses[StatusCodes.Status403Forbidden.ToString()] = new OpenApiResponse
                {
                    Description = "The authenticated user does not have access to this resource"
                };
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
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
                        AuthorizationUrl = new System.Uri(Paths.BackOfficeApi.AuthorizationEndpoint, UriKind.Relative),
                        TokenUrl = new System.Uri(Paths.BackOfficeApi.TokenEndpoint, UriKind.Relative),
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

        private static bool InjectsAuthorizationService(Type? type)
        {
            if (type is null)
            {
                return false;
            }

            return type.GetConstructors()
                .Any(ctor => ctor.GetParameters()
                    .Any(parameter => parameter.ParameterType == typeof(IAuthorizationService)));
        }
    }
}
#endif
