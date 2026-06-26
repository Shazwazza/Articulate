#nullable enable
using Articulate.Options;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// Model for the authors directory page.
    /// </summary>
    public class AuthorDirectoryModel(
        IPublishedContent content,
        IPublishedValueFallback publishedValueFallback,
        ArticulateCommentsOptions? commentsOptions = null)
        : MasterModel(content, publishedValueFallback, commentsOptions)
    {
        /// <summary>
        /// Gets or sets the author items shown in the directory.
        /// </summary>
        public IEnumerable<AuthorDirectoryItemModel>? Authors { get; set; }
    }
}
