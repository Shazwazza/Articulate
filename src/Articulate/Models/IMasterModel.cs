#nullable enable
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// Represents the master model for Articulate pages.
    /// </summary>
    public interface IMasterModel : IPublishedContent
    {
        /// <summary>
        /// Gets the current theme.
        /// </summary>
        public string Theme { get; }

        /// <summary>
        /// Gets the root blog node.
        /// </summary>
        public IPublishedContent RootBlogNode { get; }

        /// <summary>
        /// Gets the blog archive node.
        /// </summary>
        public IPublishedContent BlogArchiveNode { get; }

        /// <summary>
        /// Gets the blog title.
        /// </summary>
        public string BlogTitle { get; }

        /// <summary>
        /// Gets the blog description.
        /// </summary>
        public string BlogDescription { get; }

        /// <summary>
        /// Gets the blog logo URL.
        /// </summary>
        public string BlogLogo { get; }

        /// <summary>
        /// Gets the blog logo URL with CSS escaping for compatibility with legacy inline style usage.
        /// </summary>
        [Obsolete("Use BlogLogo.ToCssBackgroundImageVariableValue() and consume the CSS custom property from a stylesheet. Scheduled for removal in a future release.")]
        public string? BlogLogoCss { get; }

        /// <summary>
        /// Gets the blog banner URL.
        /// </summary>
        public string BlogBanner { get; }

        /// <summary>
        /// Gets the blog banner URL with CSS escaping for compatibility with legacy inline style usage.
        /// </summary>
        [Obsolete("Use BlogBanner.ToCssBackgroundImageVariableValue() and consume the CSS custom property from a stylesheet. Scheduled for removal in a future release.")]
        public string? BlogBannerCss { get; }

        /// <summary>
        /// Gets the number of items per page.
        /// </summary>
        public int PageSize { get; }

        /// <summary>
        /// Gets the Disqus short name.
        /// </summary>
        public string DisqusShortName { get; }

        /// <summary>
        /// Gets the custom RSS feed URL.
        /// </summary>
        public string CustomRssFeed { get; }

        /// <summary>
        /// Gets the current page title.
        /// </summary>
        public string PageTitle { get; }

        /// <summary>
        /// Gets the current page description.
        /// </summary>
        public string PageDescription { get; }

        /// <summary>
        /// Gets the current page tags.
        /// </summary>
        public string PageTags { get; }
    }
}
