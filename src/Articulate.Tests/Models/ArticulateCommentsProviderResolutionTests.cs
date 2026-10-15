#nullable enable
using Articulate.Options;
using NUnit.Framework;

namespace Articulate.Tests.Models
{
    /// <summary>
    /// Tests <see cref="MasterModel.ResolveGiscusRequired"/>.
    /// The override picks blog-doc values over appsettings values when all four Giscus fields are populated.
    /// It falls through to appsettings when any field is empty.
    /// Provider priority (Disqus wins over Giscus) lives in the Razor view as an <c>if/else if</c> chain on <c>IsDisqusEnabled</c> / <c>IsGiscusEnabled</c>.
    /// View tests cover that logic.
    /// </summary>
    [TestFixture]
    public class ArticulateCommentsProviderResolutionTests
    {
        private static readonly GiscusCommentsOptions _appsettings = new()
        {
            DataRepo = "app/repo",
            DataRepoId = "R_app",
            DataCategory = "app/cat",
            DataCategoryId = "DIC_app",
        };

        [Test]
        public void ResolveGiscusRequired_UsesAppsettings_WhenAllDocValuesAreEmpty()
        {
            (string Repo, string RepoId, string Category, string CategoryId) result = MasterModel.ResolveGiscusRequired(string.Empty, string.Empty, string.Empty, string.Empty, _appsettings);

            Assert.That(result, Is.EqualTo(("app/repo", "R_app", "app/cat", "DIC_app")));
        }

        [Test]
        public void ResolveGiscusRequired_UsesDocValues_WhenAllFourArePopulated()
        {
            (string Repo, string RepoId, string Category, string CategoryId) result = MasterModel.ResolveGiscusRequired(
                "blog/repo", "R_blog", "blog/cat", "DIC_blog", _appsettings);

            Assert.That(result, Is.EqualTo(("blog/repo", "R_blog", "blog/cat", "DIC_blog")));
        }

        [TestCase("", "R_blog", "blog/cat", "DIC_blog")]
        [TestCase("blog/repo", "", "blog/cat", "DIC_blog")]
        [TestCase("blog/repo", "R_blog", "", "DIC_blog")]
        [TestCase("blog/repo", "R_blog", "blog/cat", "")]
        [TestCase("   ", "R_blog", "blog/cat", "DIC_blog")]
        public void ResolveGiscusRequired_DiscardsPartialOverride_AndUsesAppsettings(
            string docRepo, string docRepoId, string docCategory, string docCategoryId)
        {
            (string Repo, string RepoId, string Category, string CategoryId) result = MasterModel.ResolveGiscusRequired(
                docRepo, docRepoId, docCategory, docCategoryId, _appsettings);

            Assert.That(result, Is.EqualTo(("app/repo", "R_app", "app/cat", "DIC_app")));
        }
    }
}
