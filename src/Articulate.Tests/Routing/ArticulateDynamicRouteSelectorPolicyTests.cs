#nullable enable
using System.Reflection;
using Articulate.Attributes;
using Articulate.Controllers;
using Articulate.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Web.Common.Routing;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateDynamicRouteSelectorPolicyTests
    {
        [Test]
        public void AppliesToEndpoints_returns_true_for_dynamic_candidates_with_articulate_endpoint()
        {
            Endpoint[] endpoints =
            [
                EndpointWithMetadata(Mock.Of<IDynamicEndpointMetadata>(), new ArticulateDynamicRouteAttribute()),
                EndpointWithMetadata(Mock.Of<IDynamicEndpointMetadata>())
            ];

            Assert.That(new ArticulateDynamicRouteSelectorPolicy().AppliesToEndpoints(endpoints), Is.True);
        }

        [Test]
        public void AppliesToEndpoints_returns_false_when_any_candidate_is_not_dynamic()
        {
            Endpoint[] endpoints =
            [
                EndpointWithMetadata(new ArticulateDynamicRouteAttribute()),
                EndpointWithMetadata(Mock.Of<IDynamicEndpointMetadata>())
            ];

            Assert.That(new ArticulateDynamicRouteSelectorPolicy().AppliesToEndpoints(endpoints), Is.False);
        }

        [Test]
        public void AppliesToEndpoints_returns_false_without_an_articulate_endpoint()
        {
            Endpoint[] endpoints =
            [
                EndpointWithMetadata(Mock.Of<IDynamicEndpointMetadata>()),
                EndpointWithMetadata(Mock.Of<IDynamicEndpointMetadata>())
            ];

            Assert.That(new ArticulateDynamicRouteSelectorPolicy().AppliesToEndpoints(endpoints), Is.False);
        }

        [Test]
        public async Task ApplyAsync_keeps_only_articulate_candidate_after_articulate_routing_succeeds()
        {
            Endpoint articulateEndpoint = EndpointWithMetadata(new ArticulateDynamicRouteAttribute());
            Endpoint umbracoEndpoint = EndpointWithMetadata();
            CandidateSet candidates = CreateCandidates(articulateEndpoint, umbracoEndpoint);
            DefaultHttpContext context = new();
            context.Features.Set(new UmbracoRouteValues(
                Mock.Of<IPublishedRequest>(),
                new ControllerActionDescriptor
                {
                    ControllerTypeInfo = typeof(MarkdownEditorController).GetTypeInfo()
                }));

            await new ArticulateDynamicRouteSelectorPolicy().ApplyAsync(context, candidates);

            Assert.That(candidates.IsValidCandidate(0), Is.True);
            Assert.That(candidates.IsValidCandidate(1), Is.False);
        }

        [Test]
        public async Task ApplyAsync_leaves_candidates_unchanged_without_articulate_route_values()
        {
            CandidateSet candidates = CreateCandidates(
                EndpointWithMetadata(new ArticulateDynamicRouteAttribute()),
                EndpointWithMetadata());

            await new ArticulateDynamicRouteSelectorPolicy().ApplyAsync(new DefaultHttpContext(), candidates);

            Assert.That(candidates.IsValidCandidate(0), Is.True);
            Assert.That(candidates.IsValidCandidate(1), Is.True);
        }

        private static CandidateSet CreateCandidates(params Endpoint[] endpoints) =>
            new(endpoints, endpoints.Select(_ => new RouteValueDictionary()).ToArray(), new int[endpoints.Length]);

        private static Endpoint EndpointWithMetadata(params object[] metadata) =>
            new(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), null);
    }
}
