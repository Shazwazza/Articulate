#nullable enable
using Articulate.Routing;
using NUnit.Framework;

namespace Articulate.Tests.Routing
{
    [TestFixture]
    public class ArticulateRouteRefreshStateTests
    {
        [Test]
        public void CurrentVersion_is_one_by_default()
        {
            ArticulateRouteRefreshState sut = new();

            Assert.That(sut.CurrentVersion, Is.EqualTo(1));
        }

        [Test]
        public void MarkDirty_increments_and_returns_current_version()
        {
            ArticulateRouteRefreshState sut = new();

            long version = sut.MarkDirty();

            Assert.That(version, Is.EqualTo(2));
            Assert.That(sut.CurrentVersion, Is.EqualTo(version));
        }
    }
}
