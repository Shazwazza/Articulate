#nullable enable
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    ///     Model for the authors directory page.
    /// </summary>
    public class AuthorDirectoryModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback)
        : MasterModel(content, publishedValueFallback)
    {
        /// <summary>
        ///     Gets or sets the author items shown in the directory.
        /// </summary>
        public IEnumerable<AuthorDirectoryItemModel>? Authors { get; set; }
    }
}
