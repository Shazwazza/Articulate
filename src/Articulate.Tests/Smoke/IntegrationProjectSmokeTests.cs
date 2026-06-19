#nullable enable
using NUnit.Framework;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;

namespace Articulate.Tests.Smoke
{
    /// <summary>
    ///     Verifies that the Umbraco integration test infrastructure initialises without errors.
    ///     Articulate service registration is covered by <see cref="StartupSmokeTests" />.
    /// </summary>
    [TestFixture]
    [UmbracoTest(Database = UmbracoTestOptions.Database.None)]
    public class IntegrationProjectSmokeTests : UmbracoIntegrationTest
    {
        [Test]
        public void Integration_test_infrastructure_resolves_umbraco_core_services() =>
            Assert.Multiple(() =>
            {
                Assert.That(GetRequiredService<IContentService>(), Is.Not.Null);
                Assert.That(GetRequiredService<IContentTypeService>(), Is.Not.Null);
            });
    }
}
