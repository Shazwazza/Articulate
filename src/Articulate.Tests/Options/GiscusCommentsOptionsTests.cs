using Articulate.Options;
using NUnit.Framework;

namespace Articulate.Tests.Options
{
    [TestFixture]
    public class GiscusCommentsOptionsTests
    {
        [Test]
        public void Defaults_AreSensible_ForFreshInstall()
        {
            var options = new GiscusCommentsOptions();

            Assert.Multiple(() =>
            {
                Assert.That(options.ScriptSrc, Is.EqualTo("https://giscus.app/client.js"));
                Assert.That(options.DataRepo, Is.EqualTo(string.Empty));
                Assert.That(options.DataRepoId, Is.EqualTo(string.Empty));
                Assert.That(options.DataCategory, Is.EqualTo(string.Empty));
                Assert.That(options.DataCategoryId, Is.EqualTo(string.Empty));
                Assert.That(options.DataMapping, Is.EqualTo("pathname"));
                Assert.That(options.DataStrict, Is.EqualTo("0"));
                Assert.That(options.DataReactionsEnabled, Is.EqualTo("1"));
                Assert.That(options.DataEmitMetadata, Is.EqualTo("0"));
                Assert.That(options.DataInputPosition, Is.EqualTo("bottom"));
                Assert.That(options.DataTheme, Is.EqualTo("preferred_color_scheme"));
                Assert.That(options.DataLang, Is.EqualTo("en"));
                Assert.That(options.DataLoading, Is.EqualTo(string.Empty));
            });
        }

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