#nullable enable
using Articulate.Controllers;
using NUnit.Framework;

namespace Articulate.Tests.Controllers
{
    [TestFixture]
    public class ArticulateRssControllerTests
    {
        [TestCase(null, 25)]
        [TestCase(-1, 1)]
        [TestCase(25, 25)]
        [TestCase(101, 100)]
        [TestCase(int.MaxValue, 100)]
        public void NormalizeMaxItems_clamps_feed_size(int? value, int expected)
        {
            Assert.That(ArticulateRssController.NormalizeMaxItems(value), Is.EqualTo(expected));
        }
    }
}
