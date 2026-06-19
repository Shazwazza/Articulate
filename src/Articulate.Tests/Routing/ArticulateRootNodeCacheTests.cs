#nullable enable
using Articulate.Routing;
using Microsoft.AspNetCore.Mvc.Controllers;
using NUnit.Framework;
using Umbraco.Cms.Core.Routing;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRootNodeCacheTests
    {
        [Test]
        public void GetContentId_matches_equivalent_domain_name_when_ids_differ()
        {
            var sut = new ArticulateRootNodeCache(new ControllerActionDescriptor());
            sut.Add(123, [new Domain(10, "https://blog.local/", 1, string.Empty, false, 0)]);

            var contentId = sut.GetContentId(new Domain(99, "https://blog.local", 2, string.Empty, false, 0));

            Assert.That(contentId, Is.EqualTo(123));
        }

        [Test]
        public void GetContentId_returns_zero_when_domain_does_not_match()
        {
            var sut = new ArticulateRootNodeCache(new ControllerActionDescriptor());
            sut.Add(123, [new Domain(10, "blog-a.local", 1, string.Empty, false, 0)]);

            var contentId = sut.GetContentId(new Domain(99, "blog-b.local", 2, string.Empty, false, 0));

            Assert.That(contentId, Is.Zero);
        }
    }
}
