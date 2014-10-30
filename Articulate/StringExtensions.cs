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
            bool addHTTP = false;
            if (urlPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                urlPath = urlPath.TrimStartString("http://");
                addHTTP = true;
            }

            urlPath = string.Join("/",
                urlPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries)
               // .Select(x => x.Replace(' ', '-')) // routing page for tags/categories will not work
                    .Select(x => HttpUtility.UrlEncode(x).Replace("+", "%20"))
                    .WhereNotNull()
                    //we are not supporting dots in our URLs it's just too difficult to
                    // support across the board with all the different config options
                    .Select(x => x.Replace('.', '-')));

            if(addHTTP)
            {
                urlPath = "http://" + urlPath;
            }

            return urlPath;

        }


        public static string TrimStartString(this string str, string toRemove)
        {
            if ((toRemove != null))
            {
                if (toRemove.Length > 0 && str.StartsWith(toRemove, StringComparison.OrdinalIgnoreCase))
                {
                    return str.Remove(0, toRemove.Length);
                }
                else
                {
                    return str;
                }
            }
            else
            {
                return str;
            }

        }
    }
}