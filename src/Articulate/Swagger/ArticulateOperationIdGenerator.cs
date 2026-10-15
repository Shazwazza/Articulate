#nullable enable
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Articulate.Swagger
{
    /// <summary>
    /// Shared operation ID generation logic used by both the Umbraco 17 (Swashbuckle) and
    /// Umbraco 18+ (Microsoft.AspNetCore.OpenApi) lanes. Lane-specific entry points, DI,
    /// and error handling live in the handlers below; only the ID-formatting rules live here.
    /// </summary>
    internal static class ArticulateOperationIdGenerator
    {
        public static string Generate(
            ApiDescription apiDescription,
            ControllerActionDescriptor controllerActionDescriptor,
            ApiVersion defaultVersion)
        {
            var httpMethod = apiDescription.HttpMethod?.ToLower().ToFirstUpper() ?? "Get";
            var relativePath = apiDescription.RelativePath;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new InvalidOperationException(
                    $"There is no relative path for controller action {apiDescription.ActionDescriptor.RouteValues["controller"]}");
            }

            // Strip the version prefix (e.g. /umbraco/articulate/api/v1/tracked-reference/{id} -> tracked-reference/{id})
            var unprefixedRelativePath = ArticulateOperationIdRegexes
                .VersionPrefixRegex()
                .Replace(relativePath, string.Empty);

            // Template placeholders become "By{Param}" (e.g. tracked-reference/{id} -> tracked-reference/ById)
            var formattedOperationId = ArticulateOperationIdRegexes
                .TemplatePlaceholdersRegex()
                .Replace(unprefixedRelativePath, m => $"By{m.Groups[1].Value.ToFirstUpper()}");

            // Dashes and slashes become camelCase boundaries (tracked-reference-id -> trackedReferenceById)
            formattedOperationId = ArticulateOperationIdRegexes
                .ToCamelCaseRegex()
                .Replace(formattedOperationId, m => m.Groups[1].Value.ToUpper());

            // Append the API version suffix when the controller maps to a non-default version
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
