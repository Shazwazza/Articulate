#nullable enable
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Articulate.Swagger.V18
{
    /// <summary>
    /// Configures named <see cref="OpenApiOptions"/> for the Articulate management API document in Umbraco 18+.
    /// </summary>
    internal class ArticulateSwaggerOptions
        : IConfigureNamedOptions<OpenApiOptions>
    {
        /// <summary>
        /// Configures the named Articulate OpenAPI document metadata.
        /// </summary>
        /// <param name="name">The OpenAPI document name.</param>
        /// <param name="options">The OpenAPI options for the named document.</param>
        public void Configure(string? name, OpenApiOptions options)
        {
            if (!string.Equals(name, ArticulateConstants.ManagementApi.Name, StringComparison.Ordinal))
            {
                return;
            }

            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Version = "Latest";
                document.Info.Description = "API for the back office dashboard section Articulate, a wonderful Blog engine built on Umbraco.";
                document.Info.Contact = new OpenApiContact
                {
                    Name = "https://github.com/Shazwazza/Articulate",
                    Url = new Uri("https://github.com/Shazwazza/Articulate")
                };
                document.Info.License = new OpenApiLicense
                {
                    Name = $"MIT License, © {DateTime.Now.Year} Shannon Deminick",
                    Url = new Uri("https://opensource.org/license/MIT")
                };
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
