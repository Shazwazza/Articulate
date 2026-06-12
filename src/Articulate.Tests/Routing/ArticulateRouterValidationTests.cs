#nullable enable
using System.Collections.Concurrent;
using Articulate.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Web.Website.Routing;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRouterValidationTests
    {
        [Test]
        public void TryMatch_returns_registered_root_cache_for_matching_path()
        {
            ControllerActionDescriptor descriptor = new()
            {
                ControllerName = "ArticulateRss",
                ActionName = "Index"
            };

            ArticulateRootNodeCache rootCache = new(descriptor);
            rootCache.Add(123, []);

            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;

            routeCache.TryAdd(new ArticulateRouteTemplate(TemplateParser.Parse("/blog/rss")), rootCache);

            RouteValueDictionary routeValues = new();

            bool matched = sut.TryMatch(new PathString("/blog/rss"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.True);
            Assert.That(matchedCache, Is.SameAs(rootCache));
            Assert.That(matchedCache!.ControllerActionDescriptor.ControllerName, Is.EqualTo("ArticulateRss"));
            Assert.That(matchedCache.ControllerActionDescriptor.ActionName, Is.EqualTo("Index"));
        }

        [Test]
        public void TryMatch_returns_false_and_null_cache_when_no_registered_route_matches_path()
        {
            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;
            routeCache.TryAdd(
                new ArticulateRouteTemplate(TemplateParser.Parse("/blog/rss")),
                new ArticulateRootNodeCache(new ControllerActionDescriptor()));

            RouteValueDictionary routeValues = new();

            bool matched = sut.TryMatch(new PathString("/blog/search"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.False);
            Assert.That(matchedCache, Is.Null);
        }

        [Test]
        public void TryMatch_preserves_prepopulated_route_values_when_no_registered_route_matches_path()
        {
            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;
            routeCache.TryAdd(
                new ArticulateRouteTemplate(TemplateParser.Parse("/blog/rss")),
                new ArticulateRootNodeCache(new ControllerActionDescriptor()));

            RouteValueDictionary routeValues = new()
            {
                ["existing"] = "value"
            };

            bool matched = sut.TryMatch(new PathString("/blog/search"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.False);
            Assert.That(matchedCache, Is.Null);
            Assert.That(routeValues["existing"], Is.EqualTo("value"));
        }

        [Test]
        public void TryMatch_populates_route_values_for_parameterized_match()
        {
            ControllerActionDescriptor descriptor = new()
            {
                ControllerName = "ArticulateRss",
                ActionName = "Author"
            };

            ArticulateRootNodeCache rootCache = new(descriptor);
            rootCache.Add(123, []);

            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;
            routeCache.TryAdd(new ArticulateRouteTemplate(TemplateParser.Parse("/blog/author/{authorId}/rss")), rootCache);

            RouteValueDictionary routeValues = new();

            bool matched = sut.TryMatch(new PathString("/blog/author/alice/rss"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.True);
            Assert.That(matchedCache, Is.SameAs(rootCache));
            Assert.That(routeValues["authorId"], Is.EqualTo("alice"));
        }

        [Test]
        public void TryMatch_preserves_prepopulated_route_values_for_parameterized_match()
        {
            ControllerActionDescriptor descriptor = new()
            {
                ControllerName = "ArticulateRss",
                ActionName = "Author"
            };

            ArticulateRootNodeCache rootCache = new(descriptor);
            rootCache.Add(123, []);

            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;
            routeCache.TryAdd(new ArticulateRouteTemplate(TemplateParser.Parse("/blog/author/{authorId}/rss")), rootCache);

            RouteValueDictionary routeValues = new()
            {
                ["existing"] = "value"
            };

            bool matched = sut.TryMatch(new PathString("/blog/author/alice/rss"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.True);
            Assert.That(matchedCache, Is.SameAs(rootCache));
            Assert.That(routeValues["existing"], Is.EqualTo("value"));
            Assert.That(routeValues["authorId"], Is.EqualTo("alice"));
        }

        [Test]
        public void TryMatch_matches_correct_route_when_multiple_routes_registered_and_only_one_matches()
        {
            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;

            ArticulateRootNodeCache expectedCache = new(new ControllerActionDescriptor
            {
                ControllerName = "ArticulateRss",
                ActionName = "FeedXslt"
            });

            // These two routes have different segment counts, so only one can match
            // regardless of ConcurrentDictionary iteration order.
            routeCache.TryAdd(
                new ArticulateRouteTemplate(TemplateParser.Parse("/blog/author/{authorId}/rss")),
                new ArticulateRootNodeCache(new ControllerActionDescriptor
                {
                    ControllerName = "ArticulateRss",
                    ActionName = "Author"
                }));
            routeCache.TryAdd(new ArticulateRouteTemplate(TemplateParser.Parse("/blog/rss/xslt")), expectedCache);

            RouteValueDictionary routeValues = new();

            bool matched = sut.TryMatch(new PathString("/blog/rss/xslt"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.True);
            Assert.That(matchedCache, Is.SameAs(expectedCache));
            Assert.That(routeValues.ContainsKey("authorId"), Is.False, "Route values should not contain leftovers from the non-matching parameterized route.");
        }

        [Test]
        public void TryMatch_does_not_leave_route_values_when_parameterized_route_does_not_match()
        {
            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;
            routeCache.TryAdd(
                new ArticulateRouteTemplate(TemplateParser.Parse("/blog/author/{authorId}/rss")),
                new ArticulateRootNodeCache(new ControllerActionDescriptor()));

            RouteValueDictionary routeValues = new();

            bool matched = sut.TryMatch(new PathString("/blog/author/alice/posts"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.False);
            Assert.That(matchedCache, Is.Null);
            Assert.That(routeValues, Is.Empty);
        }

        [Test]
        public void TryMatch_returns_false_when_route_cache_is_empty()
        {
            ArticulateRouter sut = CreateSut();
            RouteValueDictionary routeValues = new();

            bool matched = sut.TryMatch(new PathString("/blog/rss"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.False);
            Assert.That(matchedCache, Is.Null);
            Assert.That(routeValues, Is.Empty);
        }

        [Test]
        public void TryMatch_matches_path_case_insensitively()
        {
            ControllerActionDescriptor descriptor = new()
            {
                ControllerName = "ArticulateRss",
                ActionName = "Index"
            };

            ArticulateRootNodeCache rootCache = new(descriptor);
            rootCache.Add(123, []);

            ArticulateRouter sut = CreateSut();
            ConcurrentDictionary<ArticulateRouteTemplate, ArticulateRootNodeCache> routeCache = sut.RouteCache;
            routeCache.TryAdd(new ArticulateRouteTemplate(TemplateParser.Parse("/blog/rss")), rootCache);

            RouteValueDictionary routeValues = new();

            bool matched = sut.TryMatch(new PathString("/Blog/RSS"), routeValues, out ArticulateRootNodeCache? matchedCache);

            Assert.That(matched, Is.True);
            Assert.That(matchedCache, Is.SameAs(rootCache));
        }

        private static ArticulateRouter CreateSut() =>
            new(
                Mock.Of<IControllerActionSearcher>(),
                Mock.Of<Umbraco.Cms.Infrastructure.Scoping.IScopeProvider>(),
                NullLogger<ArticulateRouter>.Instance
#if UMBRACO_18_OR_GREATER
                , Mock.Of<Umbraco.Cms.Core.Services.IDocumentUrlService>()
                , Mock.Of<Umbraco.Cms.Core.Services.Navigation.IDocumentNavigationQueryService>()
                , Mock.Of<Umbraco.Cms.Core.Services.Navigation.IPublishedContentStatusFilteringService>()
#endif
            );
    }
}
