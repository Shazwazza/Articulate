using System;
using System.Linq;
using System.Web;
using Umbraco.Core;

namespace Articulate
{
    internal static class StringExtensions
    {
        public static string SafeEncodeUrlSegments(this string urlPath)
        {
            return string.Join("/",
                urlPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => HttpUtility.UrlEncode(x).Replace("+", "%20"))
                    .WhereNotNull()
                    //we are not supporting dots in our URLs it's just too difficult to
                    // support across the board with all the different config options
                    .Select(x => x.Replace('.', '-')));
        }
    }
}