using Articulate.Models;
using ClientDependency.Core.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.WebPages;
using umbraco;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Articulate
{
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Renders the Post date with Author details if author details are supplied
        /// </summary>
        /// <param name="html"></param>        
        /// <param name="model"></param>
        /// <returns></returns>
        public static IHtmlString AuthorCitation(this HtmlHelper html, PostModel model)
        {
            var sb = new StringBuilder();            
            if (model.Author != null)
            {
                sb.Append("<span>");
                sb.Append("By ");

                //TODO: Check if the current theme has an Author.cshtml theme file otherwise don't render a link!
                //In that case we should have a 'ThemeSupport' class that will check to see what a theme supports.
                if (model.Author.BlogUrl.IsNullOrWhiteSpace())
                {
                    sb.Append(model.Author.Name);
                }
                else
                {
                    sb.Append($@"<a href=""{model.Author.BlogUrl}"">{model.Author.Name}</a>");
                }
                sb.Append("&nbsp;on&nbsp;");
                sb.Append("</span>");
            }
            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// Adds generic social meta tags
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <returns></returns>        
        public static IHtmlString SocialMetaTags(this HtmlHelper html, IMasterModel model)
        {
            var builder = new StringBuilder();
            SocialMetaTags(model, builder);

            var postModel = model as PostModel;
            if (postModel != null)
            {
                SocialMetaTags(html, postModel, builder);
            }

            return MvcHtmlString.Create(builder.ToString());
        }

        /// <summary>
        /// Adds blog post social meta tags
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        /// <remarks>
        /// Would be nice to add the Standard Template but need to get more author info in there
        /// </remarks>
        public static IHtmlString SocialMetaTags(this HtmlHelper html, PostModel model)
        {
            var builder = new StringBuilder();

            SocialMetaTags(model, builder);
            SocialMetaTags(html, model, builder);

            return MvcHtmlString.Create(builder.ToString());
        }

        private static void SocialMetaTags(HtmlHelper html, PostModel model, StringBuilder builder)
        {
            if (!model.CroppedPostImageUrl.IsNullOrWhiteSpace())
            {
                var openGraphImage = new TagBuilder("meta");
                openGraphImage.Attributes["property"] = "og:image";
                openGraphImage.Attributes["content"] = PathHelper.GetDomain(html.ViewContext.RequestContext.HttpContext.Request.Url) + model.CroppedPostImageUrl;
                builder.AppendLine(openGraphImage.ToString(TagRenderMode.SelfClosing));
            }

            if (!model.SocialMetaDescription.IsNullOrWhiteSpace() || !model.Excerpt.IsNullOrWhiteSpace())
            {
                var openGraphDesc = new TagBuilder("meta");
                openGraphDesc.Attributes["property"] = "og:description";
                openGraphDesc.Attributes["content"] = model.SocialMetaDescription.IsNullOrWhiteSpace() ? model.Excerpt : model.SocialMetaDescription;
                builder.AppendLine(openGraphDesc.ToString(TagRenderMode.SelfClosing));
            }
        }

        private static void SocialMetaTags(IPublishedContent model, StringBuilder builder)
        {
            var twitterTag = new TagBuilder("meta");
            twitterTag.Attributes["name"] = "twitter:card";
            twitterTag.Attributes["value"] = "summary";
            builder.AppendLine(twitterTag.ToString(TagRenderMode.StartTag)); //non-closing since that's just the way it is

            var openGraphTitle = new TagBuilder("meta");
            openGraphTitle.Attributes["property"] = "og:title";
            openGraphTitle.Attributes["content"] = model.Name;
            builder.AppendLine(openGraphTitle.ToString(TagRenderMode.SelfClosing));

            var openGraphType = new TagBuilder("meta");
            openGraphType.Attributes["property"] = "og:type";
            openGraphType.Attributes["content"] = "article";
            builder.AppendLine(openGraphType.ToString(TagRenderMode.SelfClosing));

            var openGraphUrl = new TagBuilder("meta");
            openGraphUrl.Attributes["property"] = "og:url";
            openGraphUrl.Attributes["content"] = model.UrlAbsolute();
            builder.AppendLine(openGraphUrl.ToString(TagRenderMode.SelfClosing));            
        }

        public static IHtmlString RenderOpenSearch(this HtmlHelper html, IMasterModel model)
        {
            var openSearchUrl = model.RootBlogNode.UrlAbsolute().EnsureEndsWith('/') + "opensearch/" + model.RootBlogNode.Id;
            var tag = $@"<link rel=""search"" type=""application/opensearchdescription+xml"" href=""{openSearchUrl}"" title=""Search {model.RootBlogNode.Name}"" >";

            return new HtmlString(tag);
        }

        public static IHtmlString RssFeed(this HtmlHelper html, IMasterModel model)
        {
            var url = model.CustomRssFeed.IsNullOrWhiteSpace()
                ? model.RootBlogNode.UrlAbsolute().EnsureEndsWith('/') + "rss"
                : model.CustomRssFeed;

            return new HtmlString(
                $@"<link rel=""alternate"" type=""application/rss+xml"" title=""RSS"" href=""{url}"" />");
        }

        public static IHtmlString AuthorRssFeed(this HtmlHelper html, AuthorModel model, UrlHelper urlHelper)
        {
            var url = urlHelper.ArticulateAuthorRssUrl(model);

            return new HtmlString(
                $@"<link rel=""alternate"" type=""application/rss+xml"" title=""RSS"" href=""{url}"" />");
        }

        public static IHtmlString AdvertiseWeblogApi(this HtmlHelper html, IMasterModel model)
        {
            var rsdUrl = model.RootBlogNode.UrlAbsolute().EnsureEndsWith('/') + "rsd/" + model.RootBlogNode.Id;
            var manifestUrl = model.RootBlogNode.UrlAbsolute().EnsureEndsWith('/') + "wlwmanifest/" + model.RootBlogNode.Id;

            return new HtmlString(
                string.Concat(
                    $@"<link type=""application/rsd+xml"" rel=""edituri"" title=""RSD"" href=""{rsdUrl}"" />",
                    Environment.NewLine,
                    $@"<link rel=""wlwmanifest"" type=""application/wlwmanifest+xml"" href=""{manifestUrl}"" />"));
        }

        public static IHtmlString MetaTags(this HtmlHelper html, IMasterModel model)
        {
            var metaTags = $@"<meta name=""description"" content=""{ model.PageDescription }"" />";

            if (!string.IsNullOrWhiteSpace(model.PageTags))
                metaTags = string.Concat(
                   metaTags,
                    Environment.NewLine,
                    $@"<meta name=""tags"" content=""{ model.PageTags }"" />"
                    );

            return new HtmlString(metaTags);
        }

        public static IHtmlString GoogleAnalyticsTracking(this HtmlHelper html, IMasterModel model)
        {
            if (model.RootBlogNode.Value<string>("googleAnalyticsId").IsNullOrWhiteSpace() == false
                && model.RootBlogNode.Value<string>("googleAnalyticsName").IsNullOrWhiteSpace() == false)
            {
                return new HtmlString(@"<script>
  (function(i,s,o,g,r,a,m){i['GoogleAnalyticsObject']=r;i[r]=i[r]||function(){
  (i[r].q=i[r].q||[]).push(arguments)},i[r].l=1*new Date();a=s.createElement(o),
  m=s.getElementsByTagName(o)[0];a.async=1;a.src=g;m.parentNode.insertBefore(a,m)
  })(window,document,'script','//www.google-analytics.com/analytics.js','ga');
  ga('create', '" + model.RootBlogNode.Value<string>("googleAnalyticsId") + @"', '" + model.RootBlogNode.Value<string>("googleAnalyticsName") + @"');
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
            return html.RequiresCssFolder(PathHelper.GetThemePath(model) + "Assets/css");
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
            if (viewData == null)
            {
                viewData = html.ViewData;
            }

            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.Partial(path, viewData);
        }

        public static IHtmlString TagCloud(this HtmlHelper html, PostTagCollection model, decimal maxWeight, int maxResults)
        {
            var tagsAndWeight = model.Select(x => new { tag = x, weight = model.GetTagWeight(x, maxWeight) })
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

        public static HelperResult TagCloud(this HtmlHelper html, PostTagCollection model, Func<PostsByTagModel, HelperResult> tagLink, decimal maxWeight, int maxResults)
        {
            return new HelperResult(writer =>
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

                    li.InnerHtml = tagLink(tag.tag).ToString();
                    ul.InnerHtml += li.ToString();
                }

                writer.Write(ul.ToString());                
            });
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
        /// <typeparam name="T"></typeparam>
        /// <param name="html"></param>
        /// <param name="collection"></param>
        /// <param name="headers"></param>
        /// <param name="cssClasses"></param>
        /// <param name="cellTemplates"></param>
        /// <returns></returns>
        public static HelperResult Table<T>(this HtmlHelper html,
            IEnumerable<T> collection,
            string[] headers,
            string[] cssClasses,
            params Func<T, HelperResult>[] cellTemplates) where T : class
        {
            return html.Table(collection, null, headers, cssClasses, cellTemplates);
        }

        /// <summary>
        /// Creates an Html table based on the collection
        /// </summary>
        public static HelperResult Table<T>(this HtmlHelper html,
            IEnumerable<T> collection,
            object htmlAttributes,
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

                var tagBuilder = new TagBuilder("table");
                if (htmlAttributes != null)
                {
                    IDictionary<string, object> atts = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);
                    tagBuilder.MergeAttributes(atts);
                }
                writer.Write(tagBuilder.ToString(TagRenderMode.StartTag));

                writer.Write("<thead>");
                writer.Write("<tr>");
                for (int i = 0; i < cols; i++)
                {
                    writer.Write("<th class='{0}'>", (cssClasses.Length - 1) >= 1 ? cssClasses[i] : "");
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