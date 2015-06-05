using Articulate.Models;

namespace Articulate
{
    public static class PathHelper
    {
        public static string GetThemePath(IMasterModel model)
        {
            var themePath = "~/App_Plugins/Articulate/Themes/{0}/";
            var path = "~/css/";
            return !string.IsNullOrEmpty(model.Theme) ? string.Format(themePath, model.Theme) : path;
        }

        public static string GetThemeViewPath(IMasterModel model, string viewName)
        {
            var themePath = "~/App_Plugins/Articulate/Themes/{0}/Views/{1}.cshtml";
            var path = "~/Views/{0}.cshtml";
            return !string.IsNullOrEmpty(model.Theme) ? string.Format(themePath, model.Theme, viewName) : string.Format(path, viewName);
        }

        public static string GetThemePartialViewPath(IMasterModel model, string viewName)
        {
            var themePath = "~/App_Plugins/Articulate/Themes/{0}/Views/Partials/{1}.cshtml";
            var path = "~/Views/Partials/{0}.cshtml";
            return !string.IsNullOrEmpty(model.Theme) ? string.Format(themePath, model.Theme, viewName) : string.Format(path, viewName);
        }
    }
}