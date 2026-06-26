#nullable enable
using Articulate.Migrations.Upgrade.V_6_0_0;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;

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

        [Test]
        public async Task RunAsync_AddsFourGiscusProperties_ToEmptyBlogTab()
        {
            PropertyGroup blogGroup = CreateBlogGroup();
            AddGiscusPerBlogProperties sut = CreateMigration(blogGroup, out Mock<IContentTypeService> contentTypeService, out Mock<IContentType> contentType);

            await sut.RunAsync();

            IPropertyType[] added = blogGroup.PropertyTypes!.ToArray();
            Assert.That(added, Has.Length.EqualTo(4));
            Assert.That(added.Select(p => p.Alias), Is.EquivalentTo(new[]
            {
                "giscusRepo", "giscusRepoId", "giscusCategory", "giscusCategoryId",
            }));
            Assert.That(added.Select(p => p.SortOrder), Is.EqualTo(AddGiscusPerBlogProperties.GiscusProperties.Select(p => p.SortOrder).ToArray()));
            Assert.That(added.Select(p => p.Key), Is.EqualTo(AddGiscusPerBlogProperties.GiscusProperties.Select(p => p.Key).ToArray()));
            contentTypeService.Verify(x => x.UpdateAsync(contentType.Object, Constants.Security.SuperUserKey), Times.Once);
        }

        [Test]
        public async Task RunAsync_PreservesExistingProperties_WhenBlogTabAlreadyHasSome()
        {
            PropertyGroup blogGroup = CreateBlogGroup();
            blogGroup.PropertyTypes = new PropertyTypeCollection(
                supportsPublishing: true,
                new IPropertyType[]
                {
                    new PropertyType(ShortStringHelper, Mock.Of<IDataType>(), "existingAlias")
                    {
                        Alias = "existingAlias",
                        Name = "Existing",
                        SortOrder = 1,
                        Key = Guid.NewGuid(),
                    },
                });

            AddGiscusPerBlogProperties sut = CreateMigration(blogGroup, out _, out _);

            await sut.RunAsync();

            // 1 existing + 4 new = 5 total
            Assert.That(blogGroup.PropertyTypes!.ToArray(), Has.Length.EqualTo(5));
            Assert.That(blogGroup.PropertyTypes!.Any(p => p.Alias == "existingAlias"), Is.True);
            Assert.That(blogGroup.PropertyTypes!.Any(p => p.Alias == "giscusRepo"), Is.True);
        }

        [Test]
        public async Task RunAsync_IsIdempotent_WhenGiscusPropertiesAlreadyExist()
        {
            PropertyGroup blogGroup = CreateBlogGroup();
            blogGroup.PropertyTypes = new PropertyTypeCollection(
                supportsPublishing: true,
                AddGiscusPerBlogProperties.GiscusProperties
                    .Select(spec => new PropertyType(ShortStringHelper, Mock.Of<IDataType>(), spec.Alias)
                    {
                        Alias = spec.Alias,
                        Name = spec.Name,
                        SortOrder = spec.SortOrder,
                        Key = spec.Key,
                    })
                    .ToArray());

            AddGiscusPerBlogProperties sut = CreateMigration(blogGroup, out _, out _);

            await sut.RunAsync();

            // Alias check skips already-present giscus properties; no duplicates
            Assert.That(blogGroup.PropertyTypes!.ToArray(), Has.Length.EqualTo(4));
        }

        [Test]
        public async Task RunAsync_ReturnsEarly_WhenArticulateContentTypeMissing()
        {
            var contentTypeService = new Mock<IContentTypeService>();
            contentTypeService.Setup(x => x.Get(ArticulateConstants.ContentType.Articulate)).Returns((IContentType?)null);

            AddGiscusPerBlogProperties sut = new(
                Mock.Of<IMigrationContext>(),
                contentTypeService.Object,
                Mock.Of<IDataTypeService>(),
                ShortStringHelper,
                NullLogger<AddGiscusPerBlogProperties>.Instance);

            await sut.RunAsync();

            contentTypeService.Verify(x => x.UpdateAsync(It.IsAny<IContentType>(), It.IsAny<Guid>()), Times.Never);
        }

        [Test]
        public async Task RunAsync_ReturnsEarly_WhenTextstringDataTypeMissing()
        {
            PropertyGroup blogGroup = CreateBlogGroup();
            var contentType = new Mock<IContentType>();
            contentType.SetupGet(x => x.PropertyGroups).Returns(new PropertyGroupCollection(new[] { blogGroup }));
            var contentTypeService = new Mock<IContentTypeService>();
            contentTypeService.Setup(x => x.Get(ArticulateConstants.ContentType.Articulate)).Returns(contentType.Object);
            var dataTypeService = new Mock<IDataTypeService>();
            dataTypeService.Setup(x => x.GetAsync(Constants.DataTypes.Guids.TextstringGuid)).ReturnsAsync((IDataType?)null);

            AddGiscusPerBlogProperties sut = new(
                Mock.Of<IMigrationContext>(),
                contentTypeService.Object,
                dataTypeService.Object,
                ShortStringHelper,
                NullLogger<AddGiscusPerBlogProperties>.Instance);

            await sut.RunAsync();

            Assert.That(blogGroup.PropertyTypes?.Count() ?? 0, Is.EqualTo(0));
            contentTypeService.Verify(x => x.UpdateAsync(It.IsAny<IContentType>(), It.IsAny<Guid>()), Times.Never);
        }

        // --- helpers ---

        private static IShortStringHelper ShortStringHelper =>
            new DefaultShortStringHelper(new DefaultShortStringHelperConfig());

        private static PropertyGroup CreateBlogGroup() =>
            new(new PropertyTypeCollection(supportsPublishing: true))
            {
                Id = 1,
                Alias = "blog",
                Name = "Blog",
                SortOrder = 0,
            };

        /// <summary>
        /// Builds a migration with a real ShortStringHelper, a blog-tab PropertyGroup exposed via
        /// the content-type mock, and a textstring data-type mock whose HasIdentity=true so the
        /// PropertyType ctor populates DataTypeId/Key.
        /// </summary>
        private static AddGiscusPerBlogProperties CreateMigration(
            PropertyGroup blogGroup,
            out Mock<IContentTypeService> contentTypeService,
            out Mock<IContentType> contentType)
        {
            contentType = new Mock<IContentType>();
            contentType.SetupGet(x => x.PropertyGroups).Returns(new PropertyGroupCollection(new[] { blogGroup }));

            contentTypeService = new Mock<IContentTypeService>();
            contentTypeService.Setup(x => x.Get(ArticulateConstants.ContentType.Articulate)).Returns(contentType.Object);

            var dataType = new Mock<IDataType>();
            dataType.SetupGet(x => x.Id).Returns(42);
            dataType.SetupGet(x => x.HasIdentity).Returns(true);

            var dataTypeService = new Mock<IDataTypeService>();
            dataTypeService.Setup(x => x.GetAsync(Constants.DataTypes.Guids.TextstringGuid)).ReturnsAsync(dataType.Object);

            return new AddGiscusPerBlogProperties(
                Mock.Of<IMigrationContext>(),
                contentTypeService.Object,
                dataTypeService.Object,
                ShortStringHelper,
                NullLogger<AddGiscusPerBlogProperties>.Instance);
        }
    }
}