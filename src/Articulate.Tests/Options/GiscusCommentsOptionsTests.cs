using Articulate.Options;
using NUnit.Framework;

namespace Articulate.Tests.Options
{
    [TestFixture]
    public class GiscusCommentsOptionsTests
    {
        [Test]
        public void DataLoading_AcceptsLazyValue()
        {
            var options = new GiscusCommentsOptions { DataLoading = "lazy" };

            Assert.That(options.DataLoading, Is.EqualTo("lazy"));
        }

        [Test]
        public void ArticulateCommentsOptions_DefaultsGiscusToNewInstance()
        {
            var options = new ArticulateCommentsOptions();

            Assert.That(options.Giscus, Is.Not.Null);
            Assert.That(options.Giscus, Is.InstanceOf<GiscusCommentsOptions>());
        }
    }
}