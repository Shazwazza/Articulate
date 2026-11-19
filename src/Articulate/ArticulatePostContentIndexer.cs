#if UMBRACO_19_OR_GREATER
#nullable enable
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Search.Indexing;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Extensions;

namespace Articulate
{
    /// <summary>
    /// Makes Articulate post tags and categories free-text searchable; Umbraco 19 indexes tags as exact-match keywords only.
    /// </summary>
    /// <remarks>
    /// note: Umbraco stores each item's indexed fields in umbracoIndexDocument and reuses them on rebuild, so changing
    /// this output needs a migration that deletes the post rows (IIndexDocumentService.DeleteAsync) before rebuilding.
    /// </remarks>
    public class ArticulatePostContentIndexer(IJsonSerializer jsonSerializer) : IContentIndexer
    {
        /// <inheritdoc/>
        public Task<IEnumerable<IndexField>> GetIndexFieldsAsync(IContentBase content, string?[] cultures, bool published, CancellationToken cancellationToken)
        {
            // Match the data types rather than post type aliases so custom post types that compose the post properties are covered.
            List<IndexField> fields = [];
            foreach (IProperty property in content.Properties.Where(p =>
                p.PropertyType.DataTypeKey == ArticulateConstants.DataType.ArticulateTagsKey
                || p.PropertyType.DataTypeKey == ArticulateConstants.DataType.ArticulateCategoriesKey))
            {
                foreach (string? culture in property.PropertyType.VariesByCulture() ? cultures : [null])
                {
                    if (property.GetValue(culture, null, published) is not string value
                        || !value.DetectIsJson()
                        || jsonSerializer.Deserialize<string[]>(value) is not { Length: > 0 } tags)
                    {
                        continue;
                    }

                    // Lowest relevance tier, matching the legacy searcher's tag/category weight.
                    fields.Add(new IndexField(property.Alias, new IndexValue { TextsR3 = tags }, culture, null));
                }
            }

            return Task.FromResult<IEnumerable<IndexField>>(fields);
        }
    }
}
#endif
