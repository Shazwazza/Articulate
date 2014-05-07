using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.WebPages;
using Articulate.Models;
using ClientDependency.Core.Mvc;
using umbraco;
using Umbraco.Core.Models;
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

        /// <summary>
        /// Renders a partial view in the current theme based on the current IMasterModel
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <param name="partialName"></param>
        /// <param name="viewModel"></param>
        /// <returns></returns>
        public static IHtmlString ThemedPartial(this HtmlHelper html, IMasterModel model, string partialName, object viewModel)
        {
            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.Partial(path, viewModel);
        }

        /// <summary>
        /// Renders a partial view in the current theme based on the current IMasterModel
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <param name="partialName"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Creates an Html table based on the collection
        /// </summary>
        public static HelperResult Table<T>(this HtmlHelper html,
            IEnumerable<T> collection,
            IEnumerable<string> headers,
            params Func<T, HelperResult>[] cellTemplates) where T : class
        {
            return new HelperResult(writer =>
            {
                var items = collection.ToArray();
                var rows = items.Count();
                var cols = headers.Count();
                if (cellTemplates.Length != cols)
                {
                    throw new InvalidOperationException("The number of cell templates must equal the number of columns defined");
                }

                //create a table based on the grid
                writer.Write("<table>");
                writer.Write("<thead>");
                writer.Write("<tr>");
                foreach (var header in headers)
                {
                    writer.Write("<th>");
                    writer.Write(header);
                    writer.Write("</th>");
                }
                writer.Write("</thead>");
                for (var rowIndex = 0; rowIndex < rows; rowIndex++)
                {
                    writer.Write("<tr>");
                    for (var colIndex = 0; colIndex < cols; colIndex++)
                    {
                        writer.Write("<td>");
                        var item = items[rowIndex];
                        if (item != null)
                        {
                            //if there's an item at that grid location, call its template
                            cellTemplates[colIndex](item).WriteTo(writer);
                        }
                        writer.Write("</td>");
                    }
                    writer.Write("</tr>");
                }
                writer.Write("</table>");
            });
        }


    }
}
