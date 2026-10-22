#nullable enable

namespace Articulate.Options
{
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
        /// the browser rejects the stylesheet. <c>Vary: Origin</c> is always set so shared
        /// caches keep a separate entry per origin.
        /// <para>
        /// Default <c>["https://giscus.app"]</c>. Override to add a self-hosted giscus
        /// domain (e.g. <c>https://comments.example.com</c>) or to widen for development.
        /// Set to an empty array to disable cross-origin serving entirely (same-origin and
        /// server-to-server calls still get <c>*</c>, so e.g. <c>curl</c> continues to work).
        /// </para>
        /// </summary>
        public string[] AllowedCorsOrigins { get; set; } = new[] { "https://giscus.app" };

        /// <summary>
        /// True when all four giscus <c>data-*</c> fields required for the embed
        /// (<see cref="DataRepo"/>, <see cref="DataRepoId"/>, <see cref="DataCategory"/>,
        /// <see cref="DataCategoryId"/>) are populated. Used to decide whether giscus is the
        /// active comment provider — e.g. by the BlogML importer, which warns when a source
        /// file contains comments that giscus (having no import endpoint) cannot migrate.
        /// </summary>
        internal bool IsFullyConfigured()
            => !string.IsNullOrWhiteSpace(DataRepo)
            && !string.IsNullOrWhiteSpace(DataRepoId)
            && !string.IsNullOrWhiteSpace(DataCategory)
            && !string.IsNullOrWhiteSpace(DataCategoryId);

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
        internal static CorsHeaderDecision ResolveCorsHeaders(string? requestOrigin, IReadOnlyList<string?>? allowedOrigins)
        {
            if (string.IsNullOrEmpty(requestOrigin))
            {
                return new CorsHeaderDecision(AllowOrigin: "*", Vary: true);
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

            return new CorsHeaderDecision(AllowOrigin: null, Vary: true);
        }
    }
}
