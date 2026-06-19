#nullable enable
using System.Text.RegularExpressions;
using System.Web;

namespace Articulate
{
    /// <summary>
    ///     Extension methods for <see cref="string" />.
    /// </summary>
    public static class StringExtensions
    {
        private static readonly Regex _newlineRegex = new(@"[\r\n]+", RegexOptions.Compiled);

        /// <summary>
        ///     Replaces newlines with spaces.
        /// </summary>
        public static string NewLinesToSpaces(this string input) =>
            _newlineRegex.Replace(input, " ");

        /// <summary>
        ///     Decodes HTML-encoded strings.
        /// </summary>
        public static string DecodeHtml(this string input) => HttpUtility.HtmlDecode(input);

        /// <summary>
        ///     Truncates a string at a word boundary.
        /// </summary>
        public static string TruncateAtWord(
            this string? text,
            int maxCharacters,
            string trailingStringIfTextCut = "&hellip;")
        {
            if (text is null || (text = text.Trim()).Length <= maxCharacters)
            {
                return text ?? string.Empty;
            }

            var trailLength = trailingStringIfTextCut is ['&', ..]
                ? 1
                : trailingStringIfTextCut.Length;
            maxCharacters = maxCharacters - trailLength >= 0
                ? maxCharacters - trailLength
                : 0;
            var pos = text.LastIndexOf(" ", maxCharacters, StringComparison.Ordinal);
            if (pos >= 0)
            {
                return text[..pos] + trailingStringIfTextCut;
            }

            return string.Empty;
        }

        /// <summary>
        ///     Encodes URL segments safely.
        /// </summary>
        public static string SafeEncodeUrlSegments(this string urlPath)
        {
            if (!urlPath.InvariantStartsWith("http://") && !urlPath.InvariantStartsWith("https://"))
            {
                return EncodePath(urlPath);
            }

            if (Uri.IsWellFormedUriString(urlPath, UriKind.Absolute))
            {
                return urlPath;
            }

            if (Uri.TryCreate(urlPath, UriKind.Absolute, out Uri? url))
            {
                return url.GetLeftPart(UriPartial.Authority) + url.AbsolutePath + url.Query;
            }

            return EncodePath(urlPath);
        }

        private static string EncodePath(string urlPath) =>
            string.Join(
                "/",
                urlPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => HttpUtility.UrlEncode(x).Replace("+", "%20"))
                    .WhereNotNull()

                    // we are not supporting dots in our URLs it's just too difficult to
                    // support across the board with all the different config options
                    .Select(x => x.Replace('.', '-')));

        /// <summary>
        ///     Gets the MIME type for an image based on its file extension.
        /// </summary>
        public static string GetImageMimeType(this string filePathOrExtension)
        {
            var ext = Path.GetExtension(filePathOrExtension).Trim('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
            {
                ext = filePathOrExtension.Trim('.').ToLowerInvariant();
            }

            return ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                _ => string.Empty
            };
        }
    }
}
