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
using Umbraco.Core.Models;
using Umbraco.Web.Models;
using TagModel = Articulate.Models.TagModel;

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

        public static IHtmlString TagCloud(this HtmlHelper html, TagListModel model)
        {
            var ul = new TagBuilder("ul");
            ul.AddCssClass("tag-cloud");
            foreach (var tag in model.Tags)
            {
                var li = new TagBuilder("li");
                li.AddCssClass("tag-cloud-" + model.GetTagWeight(tag));
                var a = new TagBuilder("a");
                a.MergeAttribute("href", tag.TagUrl);
                a.SetInnerText(tag.TagName);
                li.InnerHtml = a.ToString();
                ul.InnerHtml += li.ToString();
            }
            return MvcHtmlString.Create(ul.ToString());
        }

    }
}
