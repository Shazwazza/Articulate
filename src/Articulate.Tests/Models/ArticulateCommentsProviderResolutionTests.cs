#nullable enable
using Articulate;
using Articulate.Options;
using NUnit.Framework;
using Provider = Articulate.ArticulateConstants.Comments.Provider;

namespace Articulate.Tests.Models
{
    [TestFixture]
    public class ArticulateCommentsProviderResolutionTests
    {
        [Test]
        public void ResolveProvider_ReturnsNone_WhenNoProviderIsConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: false, giscusConfigured: false);

            Assert.That(result, Is.EqualTo(Provider.None));
        }

        [Test]
        public void ResolveProvider_ReturnsGiscus_WhenOnlyGiscusIsConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: false, giscusConfigured: true);

            Assert.That(result, Is.EqualTo(Provider.Giscus));
        }

        [Test]
        public void ResolveProvider_ReturnsDisqus_WhenOnlyDisqusIsConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: true, giscusConfigured: false);

            Assert.That(result, Is.EqualTo(Provider.Disqus));
        }

        [Test]
        public void ResolveProvider_ReturnsDisqus_WhenBothProvidersAreConfigured()
        {
            Provider result = MasterModel.ResolveProvider(disqusShortNameSet: true, giscusConfigured: true);

            Assert.That(result, Is.EqualTo(Provider.Disqus));
        }

        private static readonly GiscusCommentsOptions Appsettings = new()
        {
            DataRepo = "app/repo",
            DataRepoId = "R_app",
            DataCategory = "app/cat",
            DataCategoryId = "DIC_app",
        };

        [Test]
        public void ResolveGiscusRequired_UsesAppsettings_WhenAllDocValuesAreEmpty()
        {
            (string Repo, string RepoId, string Category, string CategoryId) result = MasterModel.ResolveGiscusRequired(string.Empty, string.Empty, string.Empty, string.Empty, Appsettings);

            Assert.That(result, Is.EqualTo(("app/repo", "R_app", "app/cat", "DIC_app")));
        }

        [Test]
        public void ResolveGiscusRequired_UsesDocValues_WhenAllFourArePopulated()
        {
            (string Repo, string RepoId, string Category, string CategoryId) result = MasterModel.ResolveGiscusRequired(
                "blog/repo", "R_blog", "blog/cat", "DIC_blog", Appsettings);

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
                docRepo, docRepoId, docCategory, docCategoryId, Appsettings);

            Assert.That(result, Is.EqualTo(("app/repo", "R_app", "app/cat", "DIC_app")));
        }
    }
}
