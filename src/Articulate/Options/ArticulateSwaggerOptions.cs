#nullable enable
#if !(NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER)
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;
#endif
using Articulate.Swagger;
using Microsoft.Extensions.Options;
#if NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
#elif NET10_0_OR_GREATER
using Microsoft.OpenApi;
#else
using Microsoft.OpenApi.Models;
#endif

namespace Articulate.Options
{
#if !(NET10_0_OR_GREATER && UMBRACO_18_OR_GREATER)
    /// <summary>
    /// Configures Articulate management API OpenAPI generation across supported Umbraco versions.
    /// </summary>
    public class ArticulateSwaggerOptions(ILogger<ArticulateSwaggerOptions> logger)
        : IConfigureOptions<SwaggerGenOptions>
    {
        /// <inheritdoc/>
        public void Configure(SwaggerGenOptions options)
        {
            var year = DateTime.Now.Year.ToString();
            options.SwaggerDoc(
                ArticulateConstants.ManagementApi.Name,
                new OpenApiInfo
                {
                    Title = "Articulate Management API",
                    Description =
                        "API for the back office dashboard section Articulate, a wonderful Blog engine built on Umbraco. ",
                    Version = "Latest",
                    Contact = new OpenApiContact
                    {
                        Name = "https://github.com/Shazwazza/Articulate",
                        Url = new Uri("https://github.com/Shazwazza/Articulate")
                    },
                    License = new OpenApiLicense
                    {
                        Name = $"MIT License, © {year} Shannon Deminick",
                        Url = new Uri("https://opensource.org/license/MIT")
                    }
                });

            try
            {
                Assembly assembly = typeof(ArticulateSwaggerOptions).Assembly;
                var xmlFile = $"{assembly.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(typeof(ArticulateSwaggerOptions).Assembly);
                }
                else
                {
                    logger.LogWarning("Articulate XML comments not available for Swagger UI");
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Articulate XML comments not available for Swagger UI");
            }

            options.OperationFilter<ArticulateOperationSecurityFilter>();
        }
    }
#else
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
                document.Info.Description = "API for the back office dashboard section Articulate, a wonderful Blog engine built on Umbraco. ";
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
#endif
}
