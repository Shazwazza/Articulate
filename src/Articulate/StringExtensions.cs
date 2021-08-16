using System;
using System.Linq;
using System.Web;
using Umbraco.Extensions;

namespace Articulate
{
    public static class StringExtensions
    {
        public static string NewLinesToSpaces(this string input)
        {
            return input.Replace("\r", " ").Replace("\n", " ").Replace("  ", "");
        }

        public static string DecodeHtml(this string text)
        {
            return HttpUtility.HtmlDecode(text);
        }

        public static string TruncateAtWord(this string text, int maxCharacters, string trailingStringIfTextCut = "&hellip;")
        {
            if (text == null || (text = text.Trim()).Length <= maxCharacters)
                return text;

            int trailLength = trailingStringIfTextCut.StartsWith("&") ? 1
                                                                      : trailingStringIfTextCut.Length;
            maxCharacters = maxCharacters - trailLength >= 0 ? maxCharacters - trailLength
                                                             : 0;
            int pos = text.LastIndexOf(" ", maxCharacters, StringComparison.Ordinal);
            if (pos >= 0)
                return text.Substring(0, pos) + trailingStringIfTextCut;

            return string.Empty;
        }

        public static string SafeEncodeUrlSegments(this string urlPath)
        {
            if (urlPath.InvariantStartsWith("http://") || urlPath.InvariantStartsWith("https://"))
            {
                if (Uri.IsWellFormedUriString(urlPath, UriKind.Absolute))
                {
                    return urlPath;
                }

                if (Uri.TryCreate(urlPath, UriKind.Absolute, out var url))
                {
                    return url.GetLeftPart(UriPartial.Authority) + url.AbsolutePath + url.Query;
                }
            }

            return EncodePath(urlPath);

        }

        private static string EncodePath(string urlPath)
        {
            return string.Join("/",
                urlPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => HttpUtility.UrlEncode(x).Replace("+", "%20"))
                    .WhereNotNull()
                //we are not supporting dots in our URLs it's just too difficult to
                // support across the board with all the different config options
                    .Select(x => x.Replace('.', '-')));
        }
    }
}
