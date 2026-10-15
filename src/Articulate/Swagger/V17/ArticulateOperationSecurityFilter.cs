#nullable enable
using Umbraco.Cms.Api.Management.OpenApi;

namespace Articulate.Swagger.V17
{
    /// <summary>
    /// Adds security requirements to Articulate API operations for Swagger documentation.
    /// </summary>
    internal class ArticulateOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        /// <summary>
        /// Gets the API name for which security requirements are applied.
        /// </summary>
        protected override string ApiName => ArticulateConstants.ManagementApi.Name;
    }
}
