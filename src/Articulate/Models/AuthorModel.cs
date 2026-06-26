#nullable enable
using Articulate.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// Model for an individual author.
    /// </summary>
    public class AuthorModel : ListModel, IImageModel
    {
        private readonly Lazy<MediaWithCrops?> _image;
        private readonly Lazy<DateTime?> _lastPostDate;

        public AuthorModel(
            IPublishedContent content,
            IEnumerable<IPublishedContent>? listItems,
            PagerModel? pager,
            int postCount,
            DateTime? lastPostDate,
            IPublishedValueFallback publishedValueFallback,
            ArticulateCommentsOptions? commentsOptions = null)
            : base(content, pager, listItems, publishedValueFallback, commentsOptions)
        {
            PostCount = postCount;
            _image = new Lazy<MediaWithCrops?>(() => Unwrap().Value<MediaWithCrops>("authorImage"), true);

            // Use Posts collection (from ListModel) instead of obsolete Children property
            _lastPostDate = new Lazy<DateTime?>(
                () => lastPostDate ?? Posts.FirstOrDefault()?.Value<DateTime>("publishedDate"),
                true);
        }

        public AuthorModel(
            IPublishedContent content,
            IEnumerable<IPublishedContent>? listItems,
            PagerModel? pager,
            int postCount,
            IPublishedValueFallback publishedValueFallback)
            : this(content, listItems, pager, postCount, null, publishedValueFallback)
        {
        }

        /// <summary>
        /// Gets the author's bio.
        /// </summary>
        public string Bio => this.Value<string>("authorBio") ?? string.Empty;

        /// <summary>
        /// Gets the author's URL.
        /// </summary>
        public string? AuthorUrl => this.Value<string>("authorUrl").ToSafeHrefUrl();

        /// <inheritdoc/>
        public MediaWithCrops? Image => _image.Value;

        /// <summary>
        /// Gets the total post count for this author.
        /// </summary>
        public int PostCount { get; }

        // We know the list of posts passed in is already ordered descending so get the first
        // Not used internally or by default themes, but exposed for custom themes

        /// <summary>
        /// Gets the date of the last post by this author.
        /// </summary>
        public DateTime? LastPostDate => _lastPostDate.Value;

        /// <inheritdoc/>
        string IImageModel.Url => this.Url();

        /// <summary>
        /// Gets the wide cropped image URL.
        /// </summary>
        public string CroppedWideUrl => Image?.GetCropUrl(cropAlias: "wide", preferFocalPoint: true, useCropDimensions: true) ?? string.Empty;
    }
}
