#nullable enable
using Articulate.ImportExport;
using Articulate.Options;
using NUnit.Framework;

namespace Articulate.Tests.ImportExport
{
    [TestFixture]
    public class BlogMlImporterIsGiscusFullyConfiguredTests
    {
        [Test]
        public void ReturnsFalse_WhenGiscusOptionsAreAllEmpty()
        {
            var options = new ArticulateCommentsOptions();

            Assert.That(BlogMlImporter.IsGiscusFullyConfigured(options), Is.False);
        }

        [Test]
        public void ReturnsFalse_WhenAnyOfTheFourRequiredFieldsIsMissing()
        {
            var options = new ArticulateCommentsOptions
            {
                Giscus = new GiscusCommentsOptions
                {
                    DataRepo = "owner/repo",
                    DataRepoId = "R_xxx",
                    DataCategory = "Announcements",
                    // DataCategoryId missing
                },
            };

            Assert.That(BlogMlImporter.IsGiscusFullyConfigured(options), Is.False);
        }

        [Test]
        public void ReturnsTrue_WhenAllFourRequiredFieldsArePopulated()
        {
            var options = new ArticulateCommentsOptions
            {
                Giscus = new GiscusCommentsOptions
                {
                    DataRepo = "owner/repo",
                    DataRepoId = "R_xxx",
                    DataCategory = "Announcements",
                    DataCategoryId = "DIC_xxx",
                },
            };

            Assert.That(BlogMlImporter.IsGiscusFullyConfigured(options), Is.True);
        }

        [Test]
        public void TreatsWhitespaceAsMissing()
        {
            var options = new ArticulateCommentsOptions
            {
                Giscus = new GiscusCommentsOptions
                {
                    DataRepo = "   ",
                    DataRepoId = "R_xxx",
                    DataCategory = "Announcements",
                    DataCategoryId = "DIC_xxx",
                },
            };

            Assert.That(BlogMlImporter.IsGiscusFullyConfigured(options), Is.False);
        }
    }
}