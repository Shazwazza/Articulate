using Articulate.Models;

namespace Articulate
{
    public static class PathHelper
    {
        public static string GetThemePath(ThemedModel model)
        {
            var path = "~/App_Plugins/Articulate/Themes/{0}/";
            return string.Format(path, model.Theme);
        }

        public static string GetThemeViewPath(ThemedModel model, string viewName)
        {
            var path = "~/App_Plugins/Articulate/Themes/{0}/Views/{1}.cshtml";
            return string.Format(path, model.Theme, viewName);
        }

        public static string GetThemePartialViewPath(ThemedModel model, string viewName)
        {
            var path = "~/App_Plugins/Articulate/Themes/{0}/Views/Partials/{1}.cshtml";
            return string.Format(path, model.Theme, viewName);
        }
    }
}