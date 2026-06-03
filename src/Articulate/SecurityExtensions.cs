#nullable enable
namespace Articulate
{
    /// <summary>
    /// Security extension methods for validating and sanitizing URLs to prevent XSS and injection attacks.
    /// </summary>
    public static class SecurityExtensions
    {
        /// <summary>
        /// Validates a URL for use in href or src attributes. Allows http/https/mailto/tel and relative URLs.
        /// </summary>
        /// <param name="url">The URL to validate.</param>
        /// <returns>The URL if safe, otherwise <c>null</c>.</returns>
        public static string? ToSafeHrefUrl(this string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            url = url.Trim();

            // Reject protocol-relative URLs (//evil.com)
            if (url.StartsWith("//"))
            {
                return null;
            }

            // Allow relative URLs
            if (url.StartsWith('/') || url.StartsWith('~') || url.StartsWith('#'))
            {
                return url;
            }

            // Try to parse as absolute URI
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                // Allow relative URLs without leading slash
                return url;
            }

            // Allow safe schemes only
            if (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeMailto ||
                uri.Scheme == "tel")
            {
                return url;
            }

            // Reject dangerous protocols (javascript:, data:, vbscript:, file:, etc.)
            return null;
        }

        /// <summary>
        /// Validates and escapes a URL for use in CSS url() functions. Only allows http/https and relative URLs.
        /// </summary>
        /// <param name="url">The URL to validate and escape.</param>
        /// <returns>The CSS-escaped URL if safe, otherwise <c>null</c>.</returns>
        public static string? ToSafeCssUrl(this string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            url = url.Trim();

            // Reject protocol-relative URLs (//evil.com)
            if (url.StartsWith("//"))
            {
                return null;
            }

            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? uri))
            {
                return null;
            }

            // For absolute URIs, only allow http and https
            if (uri.IsAbsoluteUri && uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            // Normalize the URL portion first so common unsafe URI characters are escaped before CSS escaping.
            string normalized;
            try
            {
                // AbsoluteUri preserves URI separators and query delimiters while escaping unsafe characters.
                normalized = uri.IsAbsoluteUri ? uri.AbsoluteUri :
                    // For relative URLs, percent-encode literal spaces which commonly break requests.
                    url.Replace(" ", "%20");
            }
            catch
            {
                normalized = url.Replace(" ", "%20");
            }

            // Escape characters that could break out of CSS context
            return normalized
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\0", string.Empty)
                .Replace("(", "\\(")
                .Replace(")", "\\)");
        }

        /// <summary>
        /// Produces a safe CSS custom property declaration (e.g. "--post-image: url('...');") for
        /// assigning URL-bearing background images from Razor attributes.
        /// </summary>
        /// <param name="url">The URL to validate and escape for CSS.</param>
        /// <param name="variableName">The CSS custom property name, including the leading "--".</param>
        /// <returns>The CSS custom property declaration if the URL is safe, otherwise an empty string.</returns>
        public static string ToCssBackgroundImageVariableValue(this string? url, string variableName)
        {
            if (!IsSafeCssCustomPropertyName(variableName))
            {
                throw new ArgumentException("CSS custom property names must start with '--' and contain only letters, numbers, underscores, or hyphens.", nameof(variableName));
            }

            var safe = url.ToSafeCssUrl();
            return safe is not null ? $"{variableName}: url('{safe}');" : string.Empty;
        }

        private static bool IsSafeCssCustomPropertyName(string variableName)
        {
            if (variableName.Length < 3 || !variableName.StartsWith("--"))
            {
                return false;
            }

            for (var i = 2; i < variableName.Length; i++)
            {
                var c = variableName[i];
                if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
