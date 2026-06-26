using Articulate.Migrations.Upgrade.V_6_0_0;
using NUnit.Framework;

namespace Articulate.Tests.Migrations
{
    [TestFixture]
    public class AddGiscusPerBlogPropertiesTests
    {
        [Test]
        public void GiscusProperties_ExposesFourExpectedAliases()
        {
            string[] aliases = System.Array.ConvertAll(AddGiscusPerBlogProperties.GiscusProperties, p => p.Alias);

            Assert.That(aliases, Is.EquivalentTo(new[]
            {
                "giscusRepo",
                "giscusRepoId",
                "giscusCategory",
                "giscusCategoryId",
            }));
        }

        [Test]
        public void GiscusProperties_SortOrdersAreSequentialAndUnique()
        {
            int[] sortOrders = System.Array.ConvertAll(AddGiscusPerBlogProperties.GiscusProperties, p => p.SortOrder);

            Assert.That(sortOrders, Is.Unique);
            Assert.That(sortOrders, Is.Ordered);
        }

        [Test]
        public void GiscusProperties_KeysAreUniqueAndNonEmpty()
        {
            System.Guid[] keys = System.Array.ConvertAll(AddGiscusPerBlogProperties.GiscusProperties, p => p.Key);

            Assert.That(keys, Is.Unique);
            Assert.That(keys, Has.All.Not.EqualTo(System.Guid.Empty));
        }

        [Test]
        public void BlogTabAlias_IsBlog()
        {
            // Locked in to match package.xml — changing this would orphan the migration's
            // properties on a different tab than fresh installs use.
            Assert.That(AddGiscusPerBlogProperties.BlogTabAlias, Is.EqualTo("blog"));
        }
    }
}
