using Articulate.Models;
using Microsoft.AspNetCore.Http;
using System;
using Umbraco.Extensions;

namespace Articulate
{
    public static class PathHelper
    {
        public const string ThemePath = "/App_Plugins/Articulate/Themes";
        public const string UserThemePath = "/Views/Articulate";
        public const string VirtualThemePath = "~" + ThemePath;
        public const string UserVirtualThemePath = "~" + UserThemePath;
        private const string VirtualThemePathToken = "~" + ThemePath + "/{0}/";
        private const string UserVirtualThemePathToken = "~" + UserThemePath + "/{0}/";
        private const string VirtualThemeViewPathToken = "~" + ThemePath + "/{0}/Views/{1}.cshtml";
        private const string UserVirtualThemeViewPathToken = "~" + UserThemePath + "/{0}/Views/{1}.cshtml";
        private const string VirtualThemePartialViewPathToken = "~" + ThemePath + "/{0}/Views/Partials/{1}.cshtml";
        private const string UserVirtualThemePartialViewPathToken = "~" + UserThemePath + "/{0}/Views/Partials/{1}.cshtml";

        public static string GetThemePath(string theme)
        {
            if (string.IsNullOrEmpty(theme))
            {
                throw new ArgumentException($"'{nameof(theme)}' cannot be null or empty.", nameof(theme));
            }

            return DefaultThemes.IsDefaultTheme(theme)
                ? string.Format(VirtualThemePathToken, theme)
                : string.Format(UserVirtualThemePathToken, theme);
        }

        public static string GetThemePath(IMasterModel model)
        {
            if (model.Theme.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("No theme has been set for this Articulate root, republish the root with a selected theme");
            }

            return GetThemePath(model.Theme);
        }

        public static string GetThemeViewPath(IMasterModel model, string viewName)
        {
            if (model.Theme.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("No theme has been set for this Articulate root, republish the root with a selected theme");
            }

            return DefaultThemes.IsDefaultTheme(model.Theme)
                ? string.Format(VirtualThemeViewPathToken, model.Theme, viewName)
                : string.Format(UserVirtualThemeViewPathToken, model.Theme, viewName);
        }

        public static string GetThemePartialViewPath(IMasterModel model, string viewName)
        {
            if (model.Theme.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("No theme has been set for this Articulate root, republish the root with a selected theme");
            }

            return DefaultThemes.IsDefaultTheme(model.Theme)
                ? string.Format(VirtualThemePartialViewPathToken, model.Theme, viewName)
                : string.Format(UserVirtualThemePartialViewPathToken, model.Theme, viewName);
        }

        /// <summary>
        /// Get the full domain of the current page
        /// </summary>
        public static string GetDomain(HttpRequest request)
        {
            // TODO: This doesn't take into account port?
            return request.Scheme + Uri.SchemeDelimiter + request.Host;

            //return requestUrl.Scheme +
            //    System.Uri.SchemeDelimiter +
            //    requestUrl.Authority;
        }
    }
}
