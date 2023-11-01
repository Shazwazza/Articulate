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
        public static IHtmlContent AuthorCitation(this IHtmlHelper html, PostModel model)
        {
            var builder = new HtmlContentBuilder();
            if (model.Author != null)
            {
                builder.AppendHtml("<span>");
                builder.Append("By ");

                //TODO: Check if the current theme has an Author.cshtml theme file otherwise don't render a link!
                //In that case we should have a 'ThemeSupport' class that will check to see what a theme supports.
                if (model.Author.BlogUrl.IsNullOrWhiteSpace())
                {
                    builder.Append(model.Author.Name);
                }
                else
                {
                    builder.AppendHtml($@"<a href=""{model.Author.BlogUrl}"">{model.Author.Name}</a>");
                }

                builder.AppendHtml("&nbsp;on&nbsp;");
                builder.AppendHtml("</span>");
            }

            return builder;
        }

        /// <summary>
        /// Adds generic social meta tags
        /// </summary>
        /// <param name="html"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static IHtmlContent SocialMetaTags(this IHtmlHelper html, IMasterModel model)
        {
            var builder = new HtmlContentBuilder();
            SocialMetaTags(model, builder);

            if (model is PostModel postModel)
            {
                SocialMetaTags(html, postModel, builder);
            }

            return builder;
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
        public static IHtmlContent SocialMetaTags(this IHtmlHelper html, PostModel model)
        {
            var builder = new HtmlContentBuilder();

            SocialMetaTags(model, builder);
            SocialMetaTags(html, model, builder);

            return builder;
        }

        private static void SocialMetaTags(IHtmlHelper html, PostModel model, IHtmlContentBuilder builder)
        {
            if (!model.CroppedPostImageUrl.IsNullOrWhiteSpace())
            {
                var openGraphImage = new TagBuilder("meta")
                {
                    TagRenderMode = TagRenderMode.SelfClosing
                };
                openGraphImage.Attributes["property"] = "og:image";
                openGraphImage.Attributes["content"] = PathHelper.GetDomain(html.ViewContext.HttpContext.Request) + model.CroppedPostImageUrl;
                
                builder.AppendHtml(openGraphImage);
            }

            if (!model.SocialMetaDescription.IsNullOrWhiteSpace() || !model.Excerpt.IsNullOrWhiteSpace())
            {
                var openGraphDesc = new TagBuilder("meta")
                {
                    TagRenderMode = TagRenderMode.SelfClosing
                };
                openGraphDesc.Attributes["property"] = "og:description";
                openGraphDesc.Attributes["content"] = model.SocialMetaDescription.IsNullOrWhiteSpace() ? model.Excerpt : model.SocialMetaDescription;

                builder.AppendHtml(openGraphDesc);
            }
        }

        private static void SocialMetaTags(IPublishedContent model, IHtmlContentBuilder builder)
        {
            var twitterTag = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.StartTag //non-closing since that's just the way it is
            };
            twitterTag.Attributes["name"] = "twitter:card";
            twitterTag.Attributes["content"] = "summary";
            builder.AppendHtml(twitterTag);

            var openGraphTitle = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            openGraphTitle.Attributes["property"] = "og:title";
            openGraphTitle.Attributes["content"] = model.Name;
            builder.AppendHtml(openGraphTitle);

            var openGraphType = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            openGraphType.Attributes["property"] = "og:type";
            openGraphType.Attributes["content"] = "article";
            builder.AppendHtml(openGraphType);

            var openGraphUrl = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            openGraphUrl.Attributes["property"] = "og:url";
            openGraphUrl.Attributes["content"] = model.Url(mode: UrlMode.Absolute);
            builder.AppendHtml(openGraphUrl);
        }

        public static IHtmlContent RenderOpenSearch(this IHtmlHelper html, IMasterModel model)
        {
            var openSearchUrl = model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "opensearch/" + model.RootBlogNode.Id;
            var tag = $@"<link rel=""search"" type=""application/opensearchdescription+xml"" href=""{openSearchUrl}"" title=""Search {model.RootBlogNode.Name}"" >";

            return new HtmlString(tag);
        }

        public static IHtmlContent RssFeed(this IHtmlHelper html, IMasterModel model)
        {
            var url = model.CustomRssFeed.IsNullOrWhiteSpace()
                ? model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "rss"
                : model.CustomRssFeed;

            return new HtmlString(
                $@"<link rel=""alternate"" type=""application/rss+xml"" title=""RSS"" href=""{url}"" />");
        }

        public static IHtmlContent AuthorRssFeed(this IHtmlHelper html, AuthorModel model, IUrlHelper urlHelper)
        {
            var url = urlHelper.ArticulateAuthorRssUrl(model);

            return new HtmlString(
                $@"<link rel=""alternate"" type=""application/rss+xml"" title=""RSS"" href=""{url}"" />");
        }

        public static IHtmlContent AdvertiseWeblogApi(this IHtmlHelper html, IMasterModel model)
        {
            var rsdUrl = model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "rsd/" + model.RootBlogNode.Id;
            var manifestUrl = model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "wlwmanifest/" + model.RootBlogNode.Id;

            return new HtmlString(
                string.Concat(
                    $@"<link type=""application/rsd+xml"" rel=""edituri"" title=""RSD"" href=""{rsdUrl}"" />",
                    Environment.NewLine,
                    $@"<link rel=""wlwmanifest"" type=""application/wlwmanifest+xml"" href=""{manifestUrl}"" />"));
        }

        public static IHtmlContent MetaTags(this IHtmlHelper html, IMasterModel model)
        {
            var htmlContent = new HtmlContentBuilder();            

            var metaDescriptionTag = new TagBuilder("meta")
            {
                TagRenderMode = TagRenderMode.SelfClosing
            };
            metaDescriptionTag.Attributes["name"] = "description";
            metaDescriptionTag.Attributes["content"] = model.PageDescription;
            htmlContent.AppendHtml(metaDescriptionTag);

            if (!string.IsNullOrWhiteSpace(model.PageTags))
            {
                var tagsTag = new TagBuilder("meta")
                {
                    TagRenderMode = TagRenderMode.SelfClosing
                };
                tagsTag.Attributes["name"] = "tags";
                tagsTag.Attributes["content"] = model.PageTags;
                htmlContent.AppendHtml(tagsTag);
            }

            return htmlContent;
        }

        public static IHtmlContent GoogleAnalyticsTracking(this IHtmlHelper html, IMasterModel model)
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

        public static IHtmlContent TagCloud(this IHtmlHelper html, PostTagCollection model, decimal maxWeight, int maxResults)
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

            return ul;
        }

        public static IHtmlContent TagCloud(this IHtmlHelper html, PostTagCollection model, Func<PostsByTagModel, HelperResult> tagLink, decimal maxWeight, int maxResults)
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

                    ul.WriteTo(writer, HtmlEncoder.Default);

                    return Task.CompletedTask;
                });

        public static IHtmlContent ListTags(this IHtmlHelper html, PostModel model, Func<string, HelperResult> tagLink, string delimiter = ", ")
        {
            return html.ListCategoriesOrTags(model.Tags.ToArray(), tagLink, delimiter);
        }

        public static IHtmlContent ListCategories(this IHtmlHelper html, PostModel model, Func<string, HelperResult> tagLink, string delimiter = ", ")
        {
            return html.ListCategoriesOrTags(model.Categories.ToArray(), tagLink, delimiter);
        }

        public static IHtmlContent ListCategoriesOrTags(this IHtmlHelper html, string[] items, Func<string, HelperResult> tagLink, string delimiter)
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
        public static IHtmlContent Table<T>(this IHtmlHelper html,
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
        public static IHtmlContent Table<T>(this IHtmlHelper html,
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

                    var table = new TagBuilder("table");
                    if (htmlAttributes != null)
                    {
                        IDictionary<string, object> atts = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);
                        table.MergeAttributes(atts);
                    }

                    var thead = new TagBuilder("thead");
                    var tr = new TagBuilder("tr");

                    for (int i = 0; i < cols; i++)
                    {
                        var th = new TagBuilder("th");
                        th.AddCssClass((cssClasses.Length - 1) >= 1 ? cssClasses[i] : "");
                        th.InnerHtml.SetContent(headers[i]);
                        tr.InnerHtml.AppendHtml(th);
                    }

                    thead.InnerHtml.AppendHtml(tr);

                    table.InnerHtml.AppendHtml(thead);

                    var tbody = new TagBuilder("tbody");
                    for (var rowIndex = 0; rowIndex < rows; rowIndex++)
                    {
                        var trContent = new TagBuilder("tr");

                        for (var colIndex = 0; colIndex < cols; colIndex++)
                        {
                            var tdContent = new TagBuilder("td");
                            tdContent.AddCssClass((cssClasses.Length - 1) >= 1 ? cssClasses[colIndex] : "");

                            var item = items[rowIndex];
                            if (item != null)
                            {
                                //if there's an item at that grid location, call its template
                                tdContent.InnerHtml.SetHtmlContent(cellTemplates[colIndex](item));

                                //cellTemplates[colIndex](item).WriteTo(writer, HtmlEncoder.Default);
                            }

                            trContent.InnerHtml.AppendHtml(tdContent);
                        }

                        tbody.InnerHtml.AppendHtml(trContent);
                    }

                    table.InnerHtml.AppendHtml(tbody);

                    table.WriteTo(writer, HtmlEncoder.Default);
                    return Task.CompletedTask;
                });
    }
}
