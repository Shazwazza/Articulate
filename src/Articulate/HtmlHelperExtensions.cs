using Articulate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Umbraco.Extensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using System.Threading.Tasks;
using System.Text.Encodings.Web;

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
        public static HtmlString AuthorCitation(this IHtmlHelper html, PostModel model)
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
        public static HtmlString SocialMetaTags(this IHtmlHelper html, IMasterModel model)
        {
            var builder = new StringBuilder();
            SocialMetaTags(model, builder);

            var postModel = model as PostModel;
            if (postModel != null)
            {
                SocialMetaTags(html, postModel, builder);
            }

            return new HtmlString(builder.ToString());
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
        public static HtmlString SocialMetaTags(this IHtmlHelper html, PostModel model)
        {
            var builder = new StringBuilder();

            SocialMetaTags(model, builder);
            SocialMetaTags(html, model, builder);

            return new HtmlString(builder.ToString());
        }

        private static void SocialMetaTags(IHtmlHelper html, PostModel model, StringBuilder builder)
        {
            if (!model.CroppedPostImageUrl.IsNullOrWhiteSpace())
            {
                var openGraphImage = new TagBuilder("meta")
                {
                    TagRenderMode = TagRenderMode.SelfClosing
                };
                openGraphImage.Attributes["property"] = "og:image";
                openGraphImage.Attributes["content"] = PathHelper.GetDomain(html.ViewContext.HttpContext.Request) + model.CroppedPostImageUrl;
                // TODO: I don't know this works
                builder.Append(openGraphImage);
            }

            if (!model.SocialMetaDescription.IsNullOrWhiteSpace() || !model.Excerpt.IsNullOrWhiteSpace())
            {
                var openGraphDesc = new TagBuilder("meta")
                {
                    TagRenderMode = TagRenderMode.SelfClosing
                };
                openGraphDesc.Attributes["property"] = "og:description";
                openGraphDesc.Attributes["content"] = model.SocialMetaDescription.IsNullOrWhiteSpace() ? model.Excerpt : model.SocialMetaDescription;
                // TODO: I don't know this works
                builder.Append(openGraphDesc);
            }
        }

        private static void SocialMetaTags(IPublishedContent model, StringBuilder builder)
        {
            var twitterTag = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.StartTag //non-closing since that's just the way it is
            };
            twitterTag.Attributes["name"] = "twitter:card";
            twitterTag.Attributes["content"] = "summary";
            builder.Append(twitterTag);

            var openGraphTitle = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            openGraphTitle.Attributes["property"] = "og:title";
            openGraphTitle.Attributes["content"] = model.Name;
            builder.Append(openGraphTitle);

            var openGraphType = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            openGraphType.Attributes["property"] = "og:type";
            openGraphType.Attributes["content"] = "article";
            builder.AppendLine(openGraphType.ToString());

            var openGraphUrl = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            openGraphUrl.Attributes["property"] = "og:url";
            openGraphUrl.Attributes["content"] = model.Url(mode: UrlMode.Absolute);
            builder.AppendLine(openGraphUrl.ToString());
        }

        public static HtmlString RenderOpenSearch(this IHtmlHelper html, IMasterModel model)
        {
            var openSearchUrl = model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "opensearch/" + model.RootBlogNode.Id;
            var tag = $@"<link rel=""search"" type=""application/opensearchdescription+xml"" href=""{openSearchUrl}"" title=""Search {model.RootBlogNode.Name}"" >";

            return new HtmlString(tag);
        }

        public static HtmlString RssFeed(this IHtmlHelper html, IMasterModel model)
        {
            var url = model.CustomRssFeed.IsNullOrWhiteSpace()
                ? model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "rss"
                : model.CustomRssFeed;

            return new HtmlString(
                $@"<link rel=""alternate"" type=""application/rss+xml"" title=""RSS"" href=""{url}"" />");
        }

        public static HtmlString AuthorRssFeed(this IHtmlHelper html, AuthorModel model, IUrlHelper urlHelper)
        {
            var url = urlHelper.ArticulateAuthorRssUrl(model);

            return new HtmlString(
                $@"<link rel=""alternate"" type=""application/rss+xml"" title=""RSS"" href=""{url}"" />");
        }

        public static HtmlString AdvertiseWeblogApi(this IHtmlHelper html, IMasterModel model)
        {
            var rsdUrl = model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "rsd/" + model.RootBlogNode.Id;
            var manifestUrl = model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "wlwmanifest/" + model.RootBlogNode.Id;

            return new HtmlString(
                string.Concat(
                    $@"<link type=""application/rsd+xml"" rel=""edituri"" title=""RSD"" href=""{rsdUrl}"" />",
                    Environment.NewLine,
                    $@"<link rel=""wlwmanifest"" type=""application/wlwmanifest+xml"" href=""{manifestUrl}"" />"));
        }

        public static HtmlString MetaTags(this IHtmlHelper html, IMasterModel model)
        {
            StringBuilder builder = new StringBuilder();

            var metaDescriptionTag = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            metaDescriptionTag.Attributes["name"] = "description";
            metaDescriptionTag.Attributes["content"] = model.PageDescription;
            builder.AppendLine(metaDescriptionTag.ToString());

            if (!string.IsNullOrWhiteSpace(model.PageTags))
            {
                var tagsTag = new TagBuilder("meta")
                {
                    TagRenderMode = TagRenderMode.SelfClosing
                };
                tagsTag.Attributes["name"] = "tags";
                tagsTag.Attributes["content"] = model.PageTags;
                builder.AppendLine(tagsTag.ToString());
            }

            return new HtmlString(builder.ToString());
        }

        public static HtmlString GoogleAnalyticsTracking(this IHtmlHelper html, IMasterModel model)
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

        public static IHtmlHelper RequiresThemedCss(this IHtmlHelper html, IMasterModel model, string filePath)
        {
            throw new NotImplementedException("TODO: Implement theme assets");
            //return html.RequiresCss(PathHelper.GetThemePath(model) + "Assets/css" + filePath.EnsureStartsWith('/'));
        }

        public static IHtmlHelper RequiresThemedJs(this IHtmlHelper html, IMasterModel model, string filePath)
        {
            throw new NotImplementedException("TODO: Implement theme assets");
            //return html.RequiresJs(PathHelper.GetThemePath(model) + "Assets/js" + filePath.EnsureStartsWith('/'));
        }

        public static IHtmlHelper RequiresThemedCssFolder(this IHtmlHelper html, IMasterModel model)
        {
            throw new NotImplementedException("TODO: Implement theme assets");
            //return html.RequiresCssFolder(PathHelper.GetThemePath(model) + "Assets/css");
        }

        public static IHtmlHelper RequiresThemedJsFolder(this IHtmlHelper html, IMasterModel model)
        {
            throw new NotImplementedException("TODO: Implement theme assets");
            //return html.RequiresJsFolder(PathHelper.GetThemePath(model) + "Assets/js");
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
        public static Task<IHtmlContent> ThemedPartialAsync(this IHtmlHelper html, IMasterModel model, string partialName, object viewModel, ViewDataDictionary viewData = null)
        {
            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.PartialAsync(path, viewModel, viewData);
        }

        /// <summary>
        /// Renders a partial view in the current theme based on the current IMasterModel
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <param name="partialName"></param>
        /// <param name="viewData"></param>
        /// <returns></returns>
        public static Task<IHtmlContent> ThemedPartialAsync(this IHtmlHelper html, IMasterModel model, string partialName, ViewDataDictionary viewData = null)
        {
            if (viewData == null)
            {
                viewData = html.ViewData;
            }

            var path = PathHelper.GetThemePartialViewPath(model, partialName);
            return html.PartialAsync(path, viewData);
        }

        public static HtmlString TagCloud(this IHtmlHelper html, PostTagCollection model, decimal maxWeight, int maxResults)
        {
            var tagsAndWeight = model.Select(x => new { tag = x, weight = model.GetTagWeight(x, maxWeight) })
                .OrderByDescending(x => x.weight)
                .Take(maxResults);
            //.RandomOrder(); //TODO: WB this is not in V8 & would need to be implemented in Articulate

            var ul = new TagBuilder("ul");
            ul.AddCssClass("tag-cloud");
            foreach (var tag in tagsAndWeight)
            {
                var a = new TagBuilder("a");
                a.MergeAttribute("href", tag.tag.TagUrl);
                a.MergeAttribute("title", tag.tag.TagName);
                a.InnerHtml.SetContent(tag.tag.TagName);

                var li = new TagBuilder("li");
                li.AddCssClass("tag-cloud-" + tag.weight);
                li.InnerHtml.AppendHtml(a);

                ul.InnerHtml.AppendHtml(li);
            }
            return new HtmlString(ul.ToString());
        }

        public static HelperResult TagCloud(this IHtmlHelper html, PostTagCollection model, Func<PostsByTagModel, HelperResult> tagLink, decimal maxWeight, int maxResults)
            => new HelperResult(writer =>
                {
                    var tagsAndWeight = model.Select(x => new { tag = x, weight = model.GetTagWeight(x, maxWeight) })
                        .OrderByDescending(x => x.weight)
                        .Take(maxResults);
                    //.RandomOrder(); //TODO: WB this is not in V8 & would need to be implemented in Articulate

                    var ul = new TagBuilder("ul");
                    ul.AddCssClass("tag-cloud");
                    foreach (var tag in tagsAndWeight)
                    {
                        var li = new TagBuilder("li");
                        li.AddCssClass("tag-cloud-" + tag.weight);

                        li.InnerHtml.AppendHtml(tagLink(tag.tag));

                        ul.InnerHtml.AppendHtml(li);
                    }

                    writer.Write(ul.ToString());

                    return Task.CompletedTask;
                });

        public static HelperResult ListTags(this IHtmlHelper html, PostModel model, Func<string, HelperResult> tagLink, string delimiter = ", ")
        {
            return html.ListCategoriesOrTags(model.Tags.ToArray(), tagLink, delimiter);
        }

        public static HelperResult ListCategories(this IHtmlHelper html, PostModel model, Func<string, HelperResult> tagLink, string delimiter = ", ")
        {
            return html.ListCategoriesOrTags(model.Categories.ToArray(), tagLink, delimiter);
        }

        public static HelperResult ListCategoriesOrTags(this IHtmlHelper html, string[] items, Func<string, HelperResult> tagLink, string delimiter)
            => new HelperResult(writer =>
                {
                    foreach (var tag in items)
                    {
                        tagLink(tag).WriteTo(writer, HtmlEncoder.Default);
                        if (tag != items.Last())
                        {
                            writer.Write("<span>");
                            writer.Write(delimiter);
                            writer.Write("</span>");
                        }
                    }
                    return Task.CompletedTask;
                });

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
        public static HelperResult Table<T>(this IHtmlHelper html,
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
        public static HelperResult Table<T>(this IHtmlHelper html,
            IEnumerable<T> collection,
            object htmlAttributes,
            string[] headers,
            string[] cssClasses,
            params Func<T, HelperResult>[] cellTemplates) where T : class
            => new HelperResult(writer =>
                {
                    var items = collection.ToArray();
                    var rows = items.Count();
                    var cols = headers.Count();
                    if (cellTemplates.Length != cols)
                    {
                        throw new InvalidOperationException("The number of cell templates must equal the number of columns defined");
                    }

                    // TODO: What is the point of using tag builder here???
                    var tagBuilder = new TagBuilder("table");
                    if (htmlAttributes != null)
                    {
                        IDictionary<string, object> atts = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);
                        tagBuilder.MergeAttributes(atts);
                    }
                    writer.Write(tagBuilder.RenderStartTag());

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
                                cellTemplates[colIndex](item).WriteTo(writer, HtmlEncoder.Default);
                            }
                            writer.Write("</td>");
                        }
                        writer.Write("</tr>");
                    }
                    writer.Write("</table>");

                    return Task.CompletedTask;
                });
    }
}
