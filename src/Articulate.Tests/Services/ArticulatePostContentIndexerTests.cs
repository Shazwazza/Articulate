#if UMBRACO_19_OR_GREATER
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Serialization;

namespace Articulate.Tests.Services
{
    [TestFixture]
    public class ArticulatePostContentIndexerTests
    {
        [Test]
        public async Task Indexes_Articulate_Tag_Data_Types_On_Any_Content_Type_And_Skips_Other_Tags()
        {
            var content = new Mock<IContentBase>();
            content.SetupGet(c => c.Properties).Returns(new PropertyCollection(
            [
                Property("postTags", ArticulateConstants.DataType.ArticulateTagsKey, "[\"umbraco\",\"blog\"]"),
                Property("postCategories", ArticulateConstants.DataType.ArticulateCategoriesKey, "[\"news\"]"),
                Property("tags", Guid.NewGuid(), "[\"other\"]"),
                Property("legacyTags", ArticulateConstants.DataType.ArticulateTagsKey, "umbraco,blog"),
            ]));

            var serializer = new Mock<IJsonSerializer>();
            serializer.Setup(s => s.Deserialize<string[]>(It.IsAny<string>())).Returns((string json) => System.Text.Json.JsonSerializer.Deserialize<string[]>(json));

            IndexField[] fields = (await new ArticulatePostContentIndexer(serializer.Object)
                .GetIndexFieldsAsync(content.Object, [null], true, CancellationToken.None)).ToArray();

            Assert.That(fields.Select(f => (f.FieldName, string.Join(",", f.Value.TextsR3!))), Is.EquivalentTo(new[]
            {
                ("postTags", "umbraco,blog"),
                ("postCategories", "news"),
            }));
        }

        private static IProperty Property(string alias, Guid dataTypeKey, string value)
        {
            var propertyType = new Mock<IPropertyType>();
            propertyType.SetupGet(p => p.Alias).Returns(alias);
            propertyType.SetupGet(p => p.DataTypeKey).Returns(dataTypeKey);
            propertyType.SetupGet(p => p.Variations).Returns(ContentVariation.Nothing);

            var property = new Mock<IProperty>();
            property.SetupGet(p => p.Alias).Returns(alias);
            property.SetupGet(p => p.PropertyType).Returns(propertyType.Object);
            property.Setup(p => p.GetValue(null, null, true)).Returns(value);
            return property.Object;
        }
    }
}
#endif
