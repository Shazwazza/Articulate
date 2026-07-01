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
        /// Origins permitted to fetch the proxied <c>giscus.css</c> cross-origin from
        /// <c>/articulate/giscus-theme/{theme}</c>. The request's <c>Origin</c> header is
        /// echoed back as <c>Access-Control-Allow-Origin</c> when it matches an entry
        /// (case-insensitive, exact); otherwise the response omits the CORS header and
        /// the browser rejects the stylesheet. <c>Vary: Origin</c> is set on reflected
        /// responses so shared caches do not poison the allow-origin per origin.
        /// <para>
        /// Default <c>["https://giscus.app"]</c>. Override to add a self-hosted giscus
        /// domain (e.g. <c>https://comments.example.com</c>) or to widen for development.
        /// Set to an empty array to disable cross-origin serving entirely (same-origin and
        /// server-to-server calls still get <c>*</c>, so e.g. <c>curl</c> continues to work).
        /// </para>
        /// </summary>
        public string[] AllowedCorsOrigins { get; set; } = new[] { "https://giscus.app" };

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

        /// <summary>
        /// Computes the <c>Access-Control-Allow-Origin</c> + <c>Vary</c> response headers
        /// for the giscus CSS proxy. Pure static so it is trivially unit-testable.
        /// </summary>
        /// <param name="requestOrigin">
        /// The <c>Origin</c> header from the incoming request. Null or empty means the
        /// request is same-origin navigation or a server-to-server call (curl, tooling);
        /// in that case a wildcard is safe because there is no cross-origin context to
        /// gate.
        /// </param>
        /// <param name="allowedOrigins">
        /// The configured allowlist from <see cref="AllowedCorsOrigins"/>. Matched
        /// case-insensitive, exact-equality. A null or empty list means "deny everything
        /// cross-origin" — same-origin / no-Origin callers still receive <c>*</c>.
        /// </param>
        public static CorsHeaderDecision ResolveCorsHeaders(string? requestOrigin, IReadOnlyList<string?>? allowedOrigins)
        {
            if (string.IsNullOrEmpty(requestOrigin))
            {
                return new CorsHeaderDecision(AllowOrigin: "*", Vary: false);
            }

            if (allowedOrigins is not null)
            {
                foreach (string? allowed in allowedOrigins)
                {
                    if (!string.IsNullOrEmpty(allowed) &&
                        string.Equals(allowed, requestOrigin, StringComparison.OrdinalIgnoreCase))
                    {
                        return new CorsHeaderDecision(AllowOrigin: requestOrigin, Vary: true);
                    }
                }
            }

            return new CorsHeaderDecision(AllowOrigin: null, Vary: false);
        }
    }

    /// <summary>
    /// Outcome of <see cref="GiscusCommentsOptions.ResolveCorsHeaders"/>. <c>AllowOrigin</c>
    /// null means "do not emit an <c>Access-Control-Allow-Origin</c> header" (the browser
    /// will block the response). <c>Vary</c> is true only when <c>AllowOrigin</c> was
    /// reflected from the request, so shared caches cannot serve one origin's allow-list
    /// to another origin's request.
    /// </summary>
    public sealed record CorsHeaderDecision(string? AllowOrigin, bool Vary);
}
