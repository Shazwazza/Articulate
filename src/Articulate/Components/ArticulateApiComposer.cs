#nullable enable
using Articulate.Options;
using Articulate.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.OpenApi;
#if UMBRACO_18_OR_GREATER
using Articulate.Swagger.V18;
using Umbraco.Cms.Api.Management.OpenApi;
#else
using Articulate.Swagger.V17;
#endif
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;

namespace Articulate.Components
{
    /// <summary>
    /// Composes the Articulate API by registering version-appropriate OpenAPI configuration and application services.
    /// </summary>
    public class ArticulateApiComposer : IComposer
    {
        /// <summary>
        /// Registers services and configures Articulate OpenAPI behavior for the active Umbraco runtime.
        /// </summary>
        /// <param name="builder">The Umbraco builder used for service registration.</param>
        public void Compose(IUmbracoBuilder builder)
        {
            IServiceCollection services = builder.Services;
#if !UMBRACO_18_OR_GREATER
            _ = services.AddSingleton<IOperationIdHandler, ArticulateOperationIdHandler>();
            _ = services.ConfigureOptions<ArticulateSwaggerOptions>();
#else
            _ = services.ConfigureOptions<ArticulateSwaggerOptions>();
            _ = builder.AddBackOfficeOpenApiDocument(
                ArticulateConstants.ManagementApi.Name,
                document => document
                    .WithTitle("Articulate Management API")
                    .WithBackOfficeAuthentication());
#endif
            _ = services.Configure<ArticulateOpenIdClientOptions>(
                builder.Config.GetSection(ArticulateOpenIdClientOptions.SectionName));
            _ = services.AddSingleton<IValidateOptions<ArticulateOpenIdClientOptions>, ArticulateOpenIdClientOptionsValidator>();
            _ = builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ArticulateApplicationManager>();
        }
    }
}
