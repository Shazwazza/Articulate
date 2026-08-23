#nullable enable
using System.Reflection;
using Articulate.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using NUnit.Framework;
#if !UMBRACO_18_OR_GREATER
using Articulate.Swagger.V17;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
#endif

namespace Articulate.Tests.Controllers.Api
{
    [TestFixture]
    public class ApiControllerResponseAttributeTests
    {
        [Test]
        public void BlogMl_post_initialize_declares_bad_request_response_for_empty_upload()
        {
            MethodInfo method = typeof(BlogMlApiController).GetMethod(nameof(BlogMlApiController.PostInitialize))!;

            bool hasBadRequestResponse = method.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Any(attribute => attribute.StatusCode == StatusCodes.Status400BadRequest);

            Assert.That(hasBadRequestResponse, Is.True);
        }

        [Test]
        public void Markdown_editor_create_post_declares_internal_server_error_response()
        {
            MethodInfo method = typeof(MarkdownEditorApiController).GetMethod(nameof(MarkdownEditorApiController.CreatePost))!;

            bool hasServerErrorResponse = method.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Any(attribute => attribute.StatusCode == StatusCodes.Status500InternalServerError);

            Assert.That(hasServerErrorResponse, Is.True);
        }

#if !UMBRACO_18_OR_GREATER
        [Test]
        public void BlogMl_swagger_document_generates_without_duplicate_security_responses()
        {
            ServiceCollection services = new();
            _ = services.AddLogging();
            Mock<IWebHostEnvironment> webHostEnvironment = new();
            webHostEnvironment.SetupGet(x => x.ApplicationName).Returns(typeof(BlogMlApiController).Assembly.GetName().Name!);
            webHostEnvironment.SetupGet(x => x.EnvironmentName).Returns("Testing");
            webHostEnvironment.SetupGet(x => x.ContentRootPath).Returns(AppContext.BaseDirectory);
            _ = services.AddSingleton(webHostEnvironment.Object);
            _ = services.AddSingleton<IHostEnvironment>(webHostEnvironment.Object);
            _ = services.AddControllers(options => options.Conventions.Add(new BackOfficeRouteTokenConvention()))
                .AddApplicationPart(typeof(BlogMlApiController).Assembly);
            _ = services.AddEndpointsApiExplorer();
            _ = services.AddSwaggerGen();
            _ = services.AddSingleton<IConfigureOptions<SwaggerGenOptions>, ArticulateSwaggerOptions>();

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            ISwaggerProvider swaggerProvider = serviceProvider.GetRequiredService<ISwaggerProvider>();

            Assert.DoesNotThrow(() => swaggerProvider.GetSwagger(ArticulateConstants.ManagementApi.Name));
        }

        private sealed class BackOfficeRouteTokenConvention : IApplicationModelConvention
        {
            public void Apply(ApplicationModel application)
            {
                foreach (ControllerModel controller in application.Controllers)
                {
                    foreach (SelectorModel selector in controller.Selectors.Concat(controller.Actions.SelectMany(action => action.Selectors)))
                    {
                        if (selector.AttributeRouteModel?.Template is { } template)
                        {
                            selector.AttributeRouteModel.Template = template.Replace("[umbracoBackOffice]", "umbraco");
                        }
                    }
                }
            }
        }
#endif
    }
}
