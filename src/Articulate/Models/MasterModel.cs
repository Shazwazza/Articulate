using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// The basic model for all articulate objects
    /// </summary>
    public class MasterModel : PublishedContentWrapped, IMasterModel
    {
        private int? _pageSize;

        /// <summary>
        /// The basic model for all articulate objects
        /// </summary>
        public MasterModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback) : base(
            content,
            publishedValueFallback) => PublishedValueFallback = publishedValueFallback;

        /// <summary>
        /// Returns the current theme
        /// </summary>
        public string Theme
        {
            get => field ??= Unwrap().Value<string>("theme", fallback: Fallback.ToAncestors);
            protected set;
        }

        /// <inheritdoc/>
        public IPublishedContent RootBlogNode
        {
            get
            {
                if (field is not null)
                {
                    return field;
                }

                IPublishedContent root = Unwrap().AncestorOrSelf(ArticulateConstants.ContentType.Articulate);
                field = root ??
                        throw new InvalidOperationException(
                            "Could not find the Articulate root document for the current rendered page");
                return field;
            }
            protected set;
        }

        /// <summary>
        /// This will return the first archive node found under the blog root
        /// </summary>
        public IPublishedContent BlogArchiveNode
        {
            get
            {
                if (field is not null)
                {
                    return field;
                }

                IEnumerable<IPublishedContent> archiveNodes =
                    RootBlogNode.Children().Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateArchive);
                IPublishedContent list = archiveNodes.FirstOrDefault();
                field = list ??
                        throw new InvalidOperationException(
                            "Could not find the ArticulateArchive document for the current rendered page");
                return field;
            }
            protected set;
        }

        /// <summary>
        /// This will return the first authors node found under the blog root
        /// </summary>
        // Not used internally or by default themes, but exposed for custom themes
        public IPublishedContent BlogAuthorsNode
        {
            get
            {
                if (field is not null)
                {
                    return field;
                }

                IEnumerable<IPublishedContent> authorNodes = RootBlogNode
                    .Children().Where(x=> x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateAuthors);
                IPublishedContent authors = authorNodes.FirstOrDefault();
                field = authors ??
                        throw new InvalidOperationException(
                            "Could not find the ArticulateAuthors document for the current rendered page");
                return field;
            }
            protected set;
        }

        /// <inheritdoc/>
        public string DisqusShortName
        {
            get => field ??= Unwrap().Value<string>("disqusShortname", fallback: Fallback.ToAncestors);
            protected set;
        }

        /// <summary>
        /// Gets whether Disqus comments are enabled and configured with a valid shortname.
        /// Validates that the DisqusShortName is not empty and contains only valid characters (alphanumeric and hyphens).
        /// </summary>
        public bool IsDisqusEnabled => !string.IsNullOrWhiteSpace(DisqusShortName)
                                       && System.Text.RegularExpressions.Regex.IsMatch(
                                           DisqusShortName,
                                           @"^[a-zA-Z0-9\-]+$");

        /// <inheritdoc/>
        public string CustomRssFeed
        {
            get => field ??= RootBlogNode.Value<string>("customRssFeedUrl");
            protected set;
        }

        /// <inheritdoc/>
        public string BlogLogo
        {
            get => field ??= RootBlogNode.Value<MediaWithCrops>("blogLogo")?.GetCropUrl(cropAlias: "square", preferFocalPoint: true, useCropDimensions: true) ?? string.Empty;
            protected set;
        }

        /// <summary>
        /// Gets the blog logo URL with CSS escaping for compatibility with legacy inline style usage.
        /// </summary>
        [Obsolete("Use BlogLogo.ToCssBackgroundImageVariableValue() and consume the CSS custom property from a stylesheet. Scheduled for removal in a future release.")]
        public string BlogLogoCss
        {
            get => field ??= BlogLogo.ToSafeCssUrl();
            protected set;
        }

        /// <inheritdoc/>
        public string BlogBanner
        {
            get => field ??= RootBlogNode.Value<MediaWithCrops>("blogBanner")?.GetCropUrl(cropAlias: "wide", preferFocalPoint: true, useCropDimensions: true) ?? string.Empty;
            protected set;
        }

        /// <summary>
        /// Gets the blog banner URL with CSS escaping for compatibility with legacy inline style usage.
        /// </summary>
        [Obsolete("Use BlogBanner.ToCssBackgroundImageVariableValue() and consume the CSS custom property from a stylesheet. Scheduled for removal in a future release.")]
        public string BlogBannerCss
        {
            get => field ??= BlogBanner.ToSafeCssUrl();
            protected set;
        }

        /// <inheritdoc/>
        public string BlogTitle
        {
            get => field ??= Unwrap().Value<string>("blogTitle", fallback: Fallback.ToAncestors);
            protected set;
        }

        /// <inheritdoc/>
        public string BlogDescription
        {
            get => field ??= Unwrap().Value<string>("blogDescription", fallback: Fallback.ToAncestors);
            protected set;
        }

        /// <inheritdoc/>
        public int PageSize
        {
            get
            {
                _pageSize ??= Unwrap().Value(
                    "pageSize",
                    fallback: Fallback.To(Fallback.Ancestors, Fallback.DefaultValue),
                    defaultValue: 10);

                return _pageSize.Value;
            }
            protected set => _pageSize = value;
        }

        /// <inheritdoc/>
        public string PageTitle
        {
            get => field ??= Name + " - " + BlogTitle;
            protected set;
        }

        /// <inheritdoc/>
        public string PageDescription
        {
            get => field ??= BlogDescription;
            protected set;
        }

        /// <inheritdoc/>
        public string PageTags { get; protected set; }

        protected IPublishedValueFallback PublishedValueFallback { get; }
    }
}
