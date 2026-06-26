#nullable enable

namespace Articulate.Options
{
    /// <summary>
    /// Comment provider settings configured from appsettings.
    /// </summary>
    public class ArticulateCommentsOptions
    {
        /// <summary>
        /// Gets or sets Giscus comment settings.
        /// </summary>
        public GiscusCommentsOptions Giscus { get; set; } = new();
    }

    /// <summary>
    /// Giscus client-side embed settings.
    /// </summary>
    public class GiscusCommentsOptions
    {
        public string ScriptSrc { get; set; } = "https://giscus.app/client.js";

        public string DataRepo { get; set; } = string.Empty;

        public string DataRepoId { get; set; } = string.Empty;

        public string DataCategory { get; set; } = string.Empty;

        public string DataCategoryId { get; set; } = string.Empty;

        public string DataMapping { get; set; } = "pathname";

        public string DataStrict { get; set; } = "0";

        public string DataReactionsEnabled { get; set; } = "1";

        public string DataEmitMetadata { get; set; } = "0";

        public string DataInputPosition { get; set; } = "bottom";

        public string DataTheme { get; set; } = "preferred_color_scheme";

        public string DataLang { get; set; } = "en";

        /// <summary>
        /// Optional. When set to <c>"lazy"</c>, giscus adds <c>loading="lazy"</c> to its
        /// generated iframe, deferring the comment-thread fetch until the user scrolls
        /// near the comments container. Default empty (eager load).
        /// </summary>
        public string DataLoading { get; set; } = string.Empty;
    }
}
