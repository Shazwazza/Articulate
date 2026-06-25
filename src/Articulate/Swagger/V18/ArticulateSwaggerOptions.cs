#nullable enable
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;

namespace Articulate.Swagger.V18
{
    /// <summary>
    /// Configures named <see cref="OpenApiOptions"/> for the Articulate management API document in Umbraco 18+.
    /// </summary>
    public class ArticulateSwaggerOptions
        : IConfigureNamedOptions<OpenApiOptions>
    {
        /// <summary>
        /// Configures the named Articulate OpenAPI document by registering operation/document transformers.
        /// </summary>
        /// <param name="name">The OpenAPI document name.</param>
        /// <param name="options">The OpenAPI options for the named document.</param>
        public void Configure(string? name, OpenApiOptions options)
        {
            if (!string.Equals(name, ArticulateConstants.ManagementApi.Name, StringComparison.Ordinal))
            {
                return;
            }

            options.AddOperationTransformer<ArticulateOperationIdHandler>();
            options.AddOperationTransformer<ArticulateOperationSecurityFilter>();
            options.AddDocumentTransformer<ArticulateOperationSecurityFilter>();
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Version = "Latest";
                document.Info.Title = "Articulate Management API";
                document.Info.Description = "API for the back office dashboard section Articulate, a wonderful Blog engine built on Umbraco.";
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Required interface member for unnamed options; intentionally unused because Articulate config is named.
        /// </summary>
        /// <param name="options">The unnamed OpenAPI options instance.</param>
        public void Configure(OpenApiOptions options)
        {
        }
    }
}
