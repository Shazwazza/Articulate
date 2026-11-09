#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace Articulate.Swagger.V17
{
    /// <summary>
    ///     Configures Articulate management API OpenAPI generation via Swashbuckle in Umbraco 17.
    /// </summary>
    public class ArticulateSwaggerOptions(ILogger<ArticulateSwaggerOptions> logger)
        : IConfigureOptions<SwaggerGenOptions>
    {
        /// <inheritdoc />
        public void Configure(SwaggerGenOptions options)
        {
            var year = DateTime.Now.Year.ToString();
            options.SwaggerDoc(
                ArticulateConstants.ManagementApi.Name,
                new OpenApiInfo
                {
                    Title = "Articulate Management API",
                    Description =
                        "API for the back office dashboard section Articulate, a wonderful Blog engine built on Umbraco.",
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
}
