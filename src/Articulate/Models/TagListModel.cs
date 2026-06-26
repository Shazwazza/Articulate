#nullable enable
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// Model for a list of tags.
    /// </summary>
    public class TagListModel : MasterModel
    {
        public TagListModel(
            IMasterModel masterModel,
            string name,
            int pageSize,
            PostTagCollection tags,
            IPublishedValueFallback publishedValueFallback)
            : base(masterModel.RootBlogNode, publishedValueFallback, masterModel.CommentsOptions)
        {
            ArgumentNullException.ThrowIfNull(masterModel);
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(tags);
            ArgumentNullException.ThrowIfNull(publishedValueFallback);

            Name = name;
            Theme = masterModel.Theme;
            RootBlogNode = masterModel.RootBlogNode;
            BlogArchiveNode = masterModel.BlogArchiveNode;
            PageSize = pageSize;
            BlogTitle = masterModel.BlogTitle;
            BlogDescription = masterModel.BlogDescription;
            Tags = tags;
            BlogBanner = masterModel.BlogBanner;
            BlogLogo = masterModel.BlogLogo;
            CustomRssFeed = masterModel.CustomRssFeed;
            PageTitle = $"{name} - {BlogTitle}";
        }

        /// <summary>
        /// Gets the collection of tags.
        /// </summary>
        public PostTagCollection Tags { get; }

        /// <inheritdoc/>
        public override string Name { get; }
    }
}
