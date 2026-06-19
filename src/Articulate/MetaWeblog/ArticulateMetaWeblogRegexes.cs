#nullable enable
using System.Text.RegularExpressions;

namespace Articulate.MetaWeblog
{
    internal static partial class ArticulateMetaWeblogRegexes
    {
        // regex finds the image placeholder markdown tag and captures the temporary URL.
        [GeneratedRegex(
            " src=(?:\"|')(?:http|https)://(?:[\\w\\d:/-]+?)(articulate/.*?)(?:\"|')",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled,
            1000)]
        public static partial Regex MediaSourceRegex();

        [GeneratedRegex(
            " href=(?:\"|')(?:http|https)://(?:[\\w\\d:/-]+?)(articulate/.*?)(?:\"|')",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled,
            1000)]
        public static partial Regex MediaHrefRegex();

        [GeneratedRegex(
            "<p[^>]*>\\s*<img[^>]*src=[\"'](?!https?://|/media/)[^\"']*[\"'][^>]*>\\s*</p>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            1000)]
        public static partial Regex InvalidImageParagraphRegex();

        [GeneratedRegex(
            "<img[^>]*src=[\"'](?!https?://|/media/)[^\"']*[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            1000)]
        public static partial Regex InvalidImageTagRegex();
    }
}
