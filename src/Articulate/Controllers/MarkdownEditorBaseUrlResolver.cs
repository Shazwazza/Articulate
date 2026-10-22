#nullable enable
using System.Diagnostics.CodeAnalysis;

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

    /// <summary>
    /// Resolves the base URL and same-origin redirect URLs used by the standalone Markdown editor.
    /// </summary>
    /// <remarks>
    /// Request headers are not trusted on their own. The request origin is used only when it matches the origin of a
    /// configured <c>Articulate:ManagementApi:OpenIddict:Client:RedirectUris</c> entry, which lets the editor work on a
    /// custom domain whose host differs from <c>UmbracoApplicationUrl</c> (for example on Umbraco Cloud).
    /// </remarks>
    internal static class MarkdownEditorBaseUrlResolver
    {
        /// <summary>
        /// Resolves the editor base URL.
        /// </summary>
        /// <param name="requestBaseUrl">The absolute request URL (scheme, host and path base), if available.</param>
        /// <param name="umbracoApplicationUrl">The configured Umbraco application URL, if any.</param>
        /// <param name="trustedRedirectUris">The configured OpenIddict client redirect URIs.</param>
        /// <param name="source">Where the returned base URL came from.</param>
        /// <returns>The base URL string, which the caller still validates.</returns>
        public static string ResolveBaseUrl(
            string? requestBaseUrl,
            string? umbracoApplicationUrl,
            IEnumerable<string>? trustedRedirectUris,
            out MarkdownEditorBaseUrlSource source)
        {
            if (TryGetTrustedRequestBaseUri(requestBaseUrl, trustedRedirectUris, out Uri? trustedRequestUri))
            {
                source = MarkdownEditorBaseUrlSource.TrustedRequestOrigin;
                return trustedRequestUri.ToString();
            }

            if (!string.IsNullOrWhiteSpace(umbracoApplicationUrl))
            {
                source = MarkdownEditorBaseUrlSource.UmbracoApplicationUrl;
                return umbracoApplicationUrl;
            }

            source = MarkdownEditorBaseUrlSource.RequestDerived;
            return requestBaseUrl ?? string.Empty;
        }

        /// <summary>
        /// Selects the post-logout redirect URL, preferring a configured URI on the same origin as the editor.
        /// </summary>
        /// <param name="baseUri">The editor base URI.</param>
        /// <param name="postLogoutRedirectUris">The configured post-logout redirect URIs.</param>
        /// <returns>The post-logout redirect URL.</returns>
        public static string GetPostLogoutRedirectUrl(Uri baseUri, IEnumerable<string>? postLogoutRedirectUris)
        {
            Uri? firstValid = null;

            foreach (string candidate in postLogoutRedirectUris ?? [])
            {
                if (!TryParseHttpUri(candidate, out Uri? redirectUri))
                {
                    continue;
                }

                if (IsSameOrigin(redirectUri, baseUri))
                {
                    return redirectUri.ToString();
                }

                firstValid ??= redirectUri;
            }

            if (firstValid is not null)
            {
                return firstValid.ToString();
            }

            return new UriBuilder(baseUri) { Path = "/", Query = string.Empty, Fragment = string.Empty }.Uri.ToString();
        }

        /// <summary>
        /// Determines whether two absolute URIs share the same scheme, host and port.
        /// </summary>
        internal static bool IsSameOrigin(Uri first, Uri second) =>
            first.IsAbsoluteUri
            && second.IsAbsoluteUri
            && Uri.Compare(
                first,
                second,
                UriComponents.SchemeAndServer,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0;

        private static bool TryGetTrustedRequestBaseUri(
            string? requestBaseUrl,
            IEnumerable<string>? trustedRedirectUris,
            [NotNullWhen(true)] out Uri? trustedRequestUri)
        {
            trustedRequestUri = null;

            if (trustedRedirectUris is null
                || !TryParseHttpUri(requestBaseUrl, out Uri? requestUri)
                || !string.IsNullOrEmpty(requestUri.UserInfo))
            {
                return false;
            }

            foreach (string candidate in trustedRedirectUris)
            {
                if (TryParseHttpUri(candidate, out Uri? redirectUri) && IsSameOrigin(requestUri, redirectUri))
                {
                    // Rebuild from parsed components so only scheme, host, port and path base are carried over.
                    trustedRequestUri = new UriBuilder(requestUri.Scheme, requestUri.Host, requestUri.Port, requestUri.AbsolutePath).Uri;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseHttpUri(string? value, [NotNullWhen(true)] out Uri? uri)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return true;
            }

            uri = null;
            return false;
        }
    }
}
