#nullable enable
using System.Reflection;
using Asp.Versioning;
using Articulate.Controllers.Api;
using Articulate.Swagger.V17;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using NUnit.Framework;
using Some.Other.Api.Controllers;
// Implicit `using Articulate;` (from UseArticulateUsings) collides with this fully-qualified path;
// the unqualified Options type would otherwise resolve to Articulate.Options.
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Articulate.Tests.Swagger.V17
{
    /// <summary>
    ///     Operation IDs drive the hey-api generated TypeScript client; a regression silently breaks
    ///     the typed client contract. v17 covers the Swashbuckle <see cref="Umbraco.Cms.Api.Common.OpenApi.OperationIdHandler"/> shape.
    /// </summary>
    [TestFixture]
    public class ArticulateOperationIdHandlerTests
    {
        [Test]
        public void Handle_generates_camelcased_operation_id_for_articulate_namespace_action()
        {
            ArticulateOperationIdHandler sut = new(OptionsFactory.Create(new ApiVersioningOptions()));

            ApiDescription description = CreateDescription(
                typeof(StubArticulateController),
                relativePath: "umbraco/management/api/v1/blogml/import-file",
                httpMethod: "POST");

            string operationId = sut.Handle(description);

            Assert.That(operationId, Is.EqualTo("PostBlogmlImportFile"));
        }

        [Test]
        public void CanHandle_returns_true_for_articulate_namespace()
        {
            ArticulateOperationIdHandler sut = new(OptionsFactory.Create(new ApiVersioningOptions()));

            bool result = sut.CanHandle(CreateDescription(typeof(StubArticulateController)));

            Assert.That(result, Is.True);
        }

        [Test]
        public void CanHandle_returns_false_for_unrelated_namespace()
        {
            ArticulateOperationIdHandler sut = new(OptionsFactory.Create(new ApiVersioningOptions()));

            bool result = sut.CanHandle(CreateDescription(typeof(StubUnrelatedController)));

            Assert.That(result, Is.False);
        }

        private static ApiDescription CreateDescription(
            Type controllerType,
            string relativePath = "umbraco/management/api/v1/blogml/import-file",
            string httpMethod = "POST")
            => new()
            {
                HttpMethod = httpMethod,
                RelativePath = relativePath,
                ActionDescriptor = new ControllerActionDescriptor
                {
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = GetSampleAction(controllerType),
                    RouteValues = new Dictionary<string, string?> { ["controller"] = "BlogMlApi" },
                },
            };

        private static MethodInfo GetSampleAction(Type controllerType) =>
            controllerType.GetMethod(nameof(StubArticulateController.SampleAction))
            ?? controllerType.GetMethod(nameof(StubUnrelatedController.SampleAction))!;
    }
}
