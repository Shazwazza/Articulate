#nullable enable
using System.Reflection;
using Articulate.Migrations.Upgrade.V_6_0_0;
using NUnit.Framework;

namespace Articulate.Tests.Migrations
{
    [TestFixture]
    public class MigrateArticulateRichTextTests
    {
        [Test]
        public void IsTinyMcePackageInstalled_returns_true_when_tinymce_assembly_is_already_loaded()
        {
            var result = MigrateArticulateRichText.IsTinyMcePackageInstalled(
                ["TinyMCE.Umbraco"],
                _ => throw new AssertionException(
                    "Assembly load should not be attempted when TinyMCE is already loaded."));

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsTinyMcePackageInstalled_returns_true_when_tinymce_assembly_can_be_loaded()
        {
            var result = MigrateArticulateRichText.IsTinyMcePackageInstalled(
                ["Some.Other.Package"],
                _ => typeof(Assembly).Assembly);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsTinyMcePackageInstalled_returns_false_when_tinymce_assembly_is_not_loaded_or_loadable()
        {
            var result = MigrateArticulateRichText.IsTinyMcePackageInstalled(
                ["Some.Other.Package"],
                _ => throw new FileNotFoundException());

            Assert.That(result, Is.False);
        }
    }
}
