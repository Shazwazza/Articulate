#nullable enable

using Articulate.Services;

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
    /// Giscus client-side embed settings. Each property maps to the matching
    /// <c>data-*</c> attribute on the giscus script tag; see <see href="https://giscus.app"/>
    /// for field meanings.
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

        /// <summary>
        /// Giscus <c>data-theme</c>. Empty (default) = derive from the active Articulate theme's
        /// <c>giscus.css</c> when available, else fall back to giscus's built-in palette. Any
        /// non-empty value (keyword e.g. <c>light</c>/<c>dark</c>/<c>preferred_color_scheme</c>, or
        /// an absolute CSS URL) is treated as explicit operator intent and used verbatim.
        /// </summary>
        public string DataTheme { get; set; } = string.Empty;

        public string DataLang { get; set; } = "en";

        /// <summary>
        /// Optional. When set to <c>"lazy"</c>, giscus adds <c>loading="lazy"</c> to its
        /// generated iframe, deferring the comment-thread fetch until the user scrolls
        /// near the comments container. Default empty (eager load).
        /// </summary>
        public string DataLoading { get; set; } = string.Empty;

        /// <summary>
        /// Resolves the giscus <c>data-theme</c> with operator-first precedence:
        /// <list type="number">
        /// <item>An explicit, non-empty <paramref name="explicitTheme"/> (keyword or URL) always wins.</item>
        /// <item>Otherwise the active theme's <paramref name="themeAssetUrl"/> is used when non-null,
        /// so giscus injects the per-theme stylesheet into its iframe.</item>
        /// <item>Otherwise giscus's built-in palette applies.</item>
        /// </list>
        /// Pure static; the caller resolves <paramref name="themeAssetUrl"/> from the live request
        /// (LB-correct) via <see cref="IArticulateThemeRepository.GetThemeAssetUrl"/>.
        /// </summary>
        public static string ResolveGiscusTheme(string explicitTheme, string? themeAssetUrl)
            => !string.IsNullOrEmpty(explicitTheme) ? explicitTheme : (themeAssetUrl ?? "preferred_color_scheme");
    }
}
