#nullable enable
using System.Reflection;
using Asp.Versioning;
using Articulate.Controllers.Api;
using Articulate.Swagger.V18;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NUnit.Framework;
using Some.Other.Api.Controllers;

namespace Articulate.Tests.Swagger.V18
{
    /// <summary>
    ///     Operation IDs drive the hey-api generated TypeScript client; a regression silently breaks
    ///     the typed client contract. v18 covers the <see cref="IOpenApiOperationTransformer"/> shape.
    /// </summary>
    [TestFixture]
    public class ArticulateOperationIdHandlerTests
    {
        [Test]
        public async Task TransformAsync_sets_operation_id_for_articulate_namespace_action()
        {
            OpenApiOperation operation = new() { OperationId = null };
            OpenApiOperationTransformerContext context = CreateContext(
                typeof(StubArticulateController),
                relativePath: "umbraco/management/api/v1/blogml/import-file",
                httpMethod: "POST");

            await new ArticulateOperationIdHandler().TransformAsync(operation, context, CancellationToken.None);

            Assert.That(operation.OperationId, Is.EqualTo("PostBlogmlImportFile"));
        }

        [Test]
        public async Task TransformAsync_leaves_operation_id_unchanged_for_unrelated_namespace()
        {
            OpenApiOperation operation = new() { OperationId = "ExistingId" };
            OpenApiOperationTransformerContext context = CreateContext(
                typeof(StubUnrelatedController),
                relativePath: "umbraco/management/api/v1/other/thing",
                httpMethod: "GET");

            await new ArticulateOperationIdHandler().TransformAsync(operation, context, CancellationToken.None);

            // Non-Articulate actions are intentionally not handled — the existing ID is preserved.
            Assert.That(operation.OperationId, Is.EqualTo("ExistingId"));
        }

        [Test]
        public async Task TransformAsync_returns_without_setting_id_when_action_is_not_a_controller_action()
        {
            OpenApiOperation operation = new() { OperationId = null };
            OpenApiOperationTransformerContext context = new()
            {
                Document = new OpenApiDocument(),
                Description = new ApiDescription
                {
                    ActionDescriptor = new ActionDescriptor(),
                    RelativePath = "umbraco/management/api/v1/blogml/import-file",
                    HttpMethod = "POST",
                },
                DocumentName = "test",
                ApplicationServices = BuildServices(),
            };

            await new ArticulateOperationIdHandler().TransformAsync(operation, context, CancellationToken.None);

            Assert.That(operation.OperationId, Is.Null);
        }

        private static OpenApiOperationTransformerContext CreateContext(
            Type controllerType,
            string relativePath,
            string httpMethod)
            => new()
            {
                Document = new OpenApiDocument(),
                Description = new ApiDescription
                {
                    HttpMethod = httpMethod,
                    RelativePath = relativePath,
                    ActionDescriptor = new ControllerActionDescriptor
                    {
                        ControllerTypeInfo = controllerType.GetTypeInfo(),
                        MethodInfo = GetSampleAction(controllerType),
                    },
                },
                DocumentName = "test",
                ApplicationServices = BuildServices(),
            };

        private static IServiceProvider BuildServices()
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.Configure<ApiVersioningOptions>(_ => { });
            return services.BuildServiceProvider();
        }

        private static MethodInfo GetSampleAction(Type controllerType) =>
            controllerType.GetMethod(nameof(StubArticulateController.SampleAction))
            ?? controllerType.GetMethod(nameof(StubUnrelatedController.SampleAction))!;
    }
}
