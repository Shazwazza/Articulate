#nullable enable

namespace Articulate.Controllers
{
    /// <summary>
    /// Identifies where the Markdown editor base URL came from.
    /// </summary>
    internal enum MarkdownEditorBaseUrlSource
    {
        /// <summary>
        /// The request origin matched the origin of a configured OpenIddict client redirect URI.
        /// </summary>
        TrustedRequestOrigin,

        /// <summary>
        /// The configured <c>Umbraco:CMS:WebRouting:UmbracoApplicationUrl</c> value.
        /// </summary>
        UmbracoApplicationUrl,

        /// <summary>
        /// The request-derived URL, used only when no application URL is configured.
        /// </summary>
        RequestDerived,
    }
}
