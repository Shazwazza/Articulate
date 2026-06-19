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
        public void MarkDirty_increments_current_version()
        {
            ArticulateRouteRefreshState sut = new();

            sut.MarkDirty();

            Assert.That(sut.CurrentVersion, Is.EqualTo(2));
        }

        [Test]
        public void MarkDirty_returns_updated_version()
        {
            ArticulateRouteRefreshState sut = new();

            var version = sut.MarkDirty();

            Assert.That(version, Is.EqualTo(2));
            Assert.That(sut.CurrentVersion, Is.EqualTo(version));
        }
    }
}
