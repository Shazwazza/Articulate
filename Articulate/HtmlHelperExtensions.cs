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
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate
{
    public static class HtmlHelperExtensions
    {
        public static IHtmlString RssFeed(this HtmlHelper html, IMasterModel model)
        {
            var url = model.CustomRssFeed.IsNullOrWhiteSpace()
                ? model.RootBlogNode.UrlWithDomain().EnsureEndsWith('/') + "rss"
                : model.CustomRssFeed;

            return new HtmlString(
                string.Format(@"<link rel=""alternate"" type=""application/rss+xml"" title=""RSS"" href=""{0}"" />",
                    url));
        }

        public static IHtmlString AdvertiseWeblogApi(this HtmlHelper html, IMasterModel model)
        {
            var rsdUrl = model.RootBlogNode.UrlWithDomain().EnsureEndsWith('/') + "rsd/" + model.RootBlogNode.Id;
            var manifestUrl = model.RootBlogNode.UrlWithDomain().EnsureEndsWith('/') + "wlwmanifest/" + model.RootBlogNode.Id;

            return new HtmlString(
                string.Concat(
                    string.Format(@"<link type=""application/rsd+xml"" rel=""edituri"" title=""RSD"" href=""{0}"" />", rsdUrl),
                    Environment.NewLine,
                    string.Format(@"<link rel=""wlwmanifest"" type=""application/wlwmanifest+xml"" href=""{0}"" />", manifestUrl)));
        }

        public static IHtmlString GoogleAnalyticsTracking(this HtmlHelper html, IMasterModel model)
        {
            if (model.RootBlogNode.GetPropertyValue<string>("googleAnalyticsId").IsNullOrWhiteSpace() == false
                && model.RootBlogNode.GetPropertyValue<string>("googleAnalyticsName").IsNullOrWhiteSpace() == false)
            {
                return new HtmlString(@"<script>
  (function(i,s,o,g,r,a,m){i['GoogleAnalyticsObject']=r;i[r]=i[r]||function(){
  (i[r].q=i[r].q||[]).push(arguments)},i[r].l=1*new Date();a=s.createElement(o),
  m=s.getElementsByTagName(o)[0];a.async=1;a.src=g;m.parentNode.insertBefore(a,m)
  })(window,document,'script','//www.google-analytics.com/analytics.js','ga');
  ga('create', '" + model.RootBlogNode.GetPropertyValue<string>("googleAnalyticsId") + @"', '" + model.RootBlogNode.GetPropertyValue<string>("googleAnalyticsName") + @"');
  ga('send', 'pageview');
</script>");
            }
            return new HtmlString(string.Empty);
        }

        public static HtmlHelper RequiresThemedCss(this HtmlHelper html, IMasterModel model, string filePath)
        {
            return html.RequiresCss(PathHelper.GetThemePath(model) + "Assets/css" + filePath.EnsureStartsWith('/'));
        }

        public static HtmlHelper RequiresThemedJs(this HtmlHelper html, IMasterModel model, string filePath)
        {
            return html.RequiresJs(PathHelper.GetThemePath(model) + "Assets/js" + filePath.EnsureStartsWith('/'));
        }

        public static HtmlHelper RequiresThemedCssFolder(this HtmlHelper html, IMasterModel model)
        {
            return html.RequiresFolder(PathHelper.GetThemePath(model) + "Assets/css", 
                100, "*.css", (absPath, pri) => html.RequiresCss(absPath, pri));

            //TODO: As per below, when latest CDF is releaesd and bundled we don't have to do this
            //return html.RequiresCssFolder(PathHelper.GetThemePath(model) + "Assets/css");
        }

        //TODO: This is only here as a hack until CDF 1.8.0 is released and shipped that fixes a bug
        private static HtmlHelper RequiresFolder(this HtmlHelper html, string folderPath, int priority, string fileSearch, Action<string, int> requiresAction)
        {
            var httpContext = html.ViewContext.HttpContext;
            var systemRootPath = httpContext.Server.MapPath("~/");
            var folderMappedPath = httpContext.Server.MapPath(folderPath);

            if (folderMappedPath.StartsWith(systemRootPath))
            {
                var files = Directory.GetFiles(folderMappedPath, fileSearch, SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var absoluteFilePath = "~/" + file.Substring(systemRootPath.Length).Replace("\\", "/");
                    requiresAction(absoluteFilePath, priority);
                    html.RequiresJs(absoluteFilePath, priority);
                }
            }

            return html;
        }

        public static HtmlHelper RequiresThemedJsFolder(this HtmlHelper html, IMasterModel model)
        {
            return html.RequiresJsFolder(PathHelper.GetThemePath(model) + "Assets/js");
        }

        /// <summary>
        /// Renders a partial view in the current theme based on the current IMasterModel
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <param name="partialName"></param>
        /// <param name="viewModel"></param>
        /// <param name="viewData"></param>
        /// <returns></returns>
        public static IHtmlString ThemedPartial(this HtmlHelper html, IMasterModel model, string partialName, object viewModel, ViewDataDictionary viewData = null)
        {
            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.Partial(path, viewModel, viewData);
        }

        /// <summary>
        /// Renders a partial view in the current theme based on the current IMasterModel
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <param name="partialName"></param>
        /// <param name="viewData"></param>
        /// <returns></returns>
        public static IHtmlString ThemedPartial(this HtmlHelper html, IMasterModel model, string partialName, ViewDataDictionary viewData = null)
        {
            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.Partial(path, viewData);
        }

        public static IHtmlString TagCloud(this HtmlHelper html, PostTagCollection model, decimal maxWeight, int maxResults)
        {
            var tagsAndWeight = model.Select(x => new {tag = x, weight = model.GetTagWeight(x, maxWeight)})
                .OrderByDescending(x => x.weight)
                .Take(maxResults)
                .RandomOrder();

            var ul = new TagBuilder("ul");
            ul.AddCssClass("tag-cloud");
            foreach (var tag in tagsAndWeight)
            {
                var li = new TagBuilder("li");
                li.AddCssClass("tag-cloud-" + tag.weight);
                var a = new TagBuilder("a");
                a.MergeAttribute("href", tag.tag.TagUrl);
                a.MergeAttribute("title", tag.tag.TagName);
                a.SetInnerText(tag.tag.TagName);
                li.InnerHtml = a.ToString();
                ul.InnerHtml += li.ToString();
            }
            return MvcHtmlString.Create(ul.ToString());
        }

        public static HelperResult ListTags(this HtmlHelper html, PostModel model, Func<string, HelperResult> tagLink, string delimiter = ", ")
        {
            return html.ListCategoriesOrTags(model.Tags.ToArray(), tagLink, delimiter);
        }

        public static HelperResult ListCategories(this HtmlHelper html, PostModel model, Func<string, HelperResult> tagLink, string delimiter = ", ")
        {
            return html.ListCategoriesOrTags(model.Categories.ToArray(), tagLink, delimiter);
        }

        public static HelperResult ListCategoriesOrTags(this HtmlHelper html, string[] items, Func<string, HelperResult> tagLink, string delimiter)
        {
            return new HelperResult(writer =>
            {
                foreach (var tag in items)
                {
                    tagLink(tag).WriteTo(writer);
                    if (tag != items.Last())
                    {
                        writer.Write("<span>");
                        writer.Write(delimiter);
                        writer.Write("</span>");
                    }
                }
            });

        }

        /// <summary>
        /// Creates an Html table based on the collection
        /// </summary>
        public static HelperResult Table<T>(this HtmlHelper html,
            IEnumerable<T> collection,
            string[] headers,
            string[] cssClasses,
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
                for (int i = 0; i < cols; i++)
                {
                    writer.Write("<th class='{0}'>", (cssClasses.Length-1) >= 1 ? cssClasses[i] : "");
                    writer.Write(headers[i]);
                    writer.Write("</th>");
                }
                writer.Write("</thead>");
                for (var rowIndex = 0; rowIndex < rows; rowIndex++)
                {
                    writer.Write("<tr>");
                    for (var colIndex = 0; colIndex < cols; colIndex++)
                    {
                        writer.Write("<td class='{0}'>", (cssClasses.Length - 1) >= 1 ? cssClasses[colIndex] : "");
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
