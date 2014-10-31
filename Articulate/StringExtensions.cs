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

            // handle relative or absolute URLs
            bool IsAbsolute;
            Uri Url = new Uri(urlPath, UriKind.RelativeOrAbsolute);
            IsAbsolute = Url.IsAbsoluteUri;
            if (!IsAbsolute) // use request variables to build a full Uri class
            {
                UriBuilder builder = new UriBuilder(HttpContext.Current.Request.Url.Scheme, HttpContext.Current.Request.Url.Host, HttpContext.Current.Request.Url.Port);
                builder.Path = VirtualPathUtility.ToAbsolute(urlPath);
                Url = builder.Uri;
            }

            if (IsAbsolute)
            {
                return Url.Scheme + "://" + Url.Host + Url.AbsolutePath.Replace('.', '-');
            }
            else
            {
                return Url.AbsolutePath.Replace('.', '-');
            }

        }
    }
}