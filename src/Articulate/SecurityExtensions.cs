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
        /// Produces a safe CSS style fragment (e.g. "background-image: url('...');") as a plain C# string
        /// suitable for assigning to a local variable in a Razor view. This avoids interpolating an
        /// HTML content value into a C# string (which loses HTML content semantics) when building
        /// inline style attribute values.
        /// </summary>
        /// <param name="url">The URL to validate and escape for CSS.</param>
        /// <returns>The full style fragment if the URL is safe, otherwise an empty string.</returns>
        public static string ToCssStyleAttributeValue(this string? url)
        {
            var safe = url.ToSafeCssUrl();
            return safe is not null ? $"background-image: url('{safe}');" : string.Empty;
        }
    }
}
