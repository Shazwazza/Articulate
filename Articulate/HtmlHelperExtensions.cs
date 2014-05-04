using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Articulate.Models;
using ClientDependency.Core.Mvc;
using Umbraco.Web.Models;

namespace Articulate
{
    public static class HtmlHelperExtensions
    {
        public static HtmlHelper RequiresThemedCssFolder(this HtmlHelper html, IMasterModel model)
        {
            return html.RequiresCssFolder(PathHelper.GetThemePath(model) + "Assets/css");
        }

        public static HtmlHelper RequiresThemedJsFolder(this HtmlHelper html, IMasterModel model)
        {
            return html.RequiresCssFolder(PathHelper.GetThemePath(model) + "Assets/js");
        }

        public static IHtmlString ThemedPartial(this HtmlHelper html, IMasterModel model, string partialName, object viewModel)
        {
            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.Partial(path, viewModel);
        }

        public static IHtmlString ThemedPartial(this HtmlHelper html, IMasterModel model, string partialName)
        {
            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.Partial(path);
        }

    }
}
