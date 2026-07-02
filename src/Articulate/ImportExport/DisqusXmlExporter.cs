#nullable enable
using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Argotic.Syndication.Specialized;
using Articulate.Services;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;

namespace Articulate.ImportExport
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    /// Exporter for blog comments to Disqus XML format.
    /// </summary>
    public class DisqusXmlExporter(
        IPublishedUrlProvider publishedUrlProvider,
        ILogger<DisqusXmlExporter> logger,
        IArticulateMarkdownConverter articulateMarkdownConverter,
        IArticulateRichTextRenderer richTextRenderer)
    {
        private const string DisqusGmtDateFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Exports the comments from a collection of posts to a Disqus-compatible XML document.
        /// </summary>
        /// <param name="posts">The posts whose comments should be exported.</param>
        /// <param name="document">The source BlogML document containing the comments.</param>
        /// <returns>An XML document in Disqus import format.</returns>
        public XDocument Export(IEnumerable<IContent> posts, BlogMLDocument document)
        {
            var nsContent = XNamespace.Get("http://purl.org/rss/1.0/modules/content/");
            var nsDsq = XNamespace.Get("http://www.disqus.com/");
            var nsDc = XNamespace.Get("http://purl.org/dc/elements/1.1/");
            var nsWp = XNamespace.Get("http://wordpress.org/export/1.0/");

            var xChannel = new XElement("channel");

            var xDoc = new XDocument(
                new XElement(
                    "rss",
                    new XAttribute("version", "2.0"),
                    new XAttribute(XNamespace.Xmlns + "content", nsContent),
                    new XAttribute(XNamespace.Xmlns + "dsq", nsDsq),
                    new XAttribute(XNamespace.Xmlns + "dc", nsDc),
                    new XAttribute(XNamespace.Xmlns + "wp", nsWp),
                    xChannel));

            foreach (IContent post in posts)
            {
                BlogMLPost? blogMlPost = FindBlogMlPost(post, document);
                if (blogMlPost is null || blogMlPost.Comments.Count == 0)
                {
                    continue;
                }

                var body = GetPostBody(post);
                DateTime publishedDate = GetPublishedDate(post);

                XElement xItem = CreatePostElement(post, body, publishedDate, nsContent, nsDsq, nsWp);

                foreach (BlogMLComment comment in blogMlPost.Comments)
                {
                    XElement xComment = CreateCommentElement(comment, nsWp);
                    xItem.Add(xComment);
                }

                xChannel.Add(xItem);
            }

            return xDoc;
        }

        private BlogMLPost? FindBlogMlPost(IContent post, BlogMLDocument document)
        {
            // First try to match by importId (most reliable)
            var importId = post.GetValue<string>("importId");
            if (!string.IsNullOrWhiteSpace(importId))
            {
                BlogMLPost? blogMlPost = document.Posts?.FirstOrDefault(x => x.Id == importId);
                if (blogMlPost is not null)
                {
                    return blogMlPost;
                }
            }

            // Fallback: try matching by name (with HTML decoding to handle special characters)
            BlogMLPost? matchedPost = document.Posts?.FirstOrDefault(x =>
                WebUtility.HtmlDecode(x.Title.Content) == post.Name);

            if (matchedPost is null)
            {
                logger.LogWarning(
                    "Cannot find blog ml post XML element with post name {PostName} (importId: {ImportId})",
                    post.Name,
                    importId ?? "none");
            }

            return matchedPost;
        }

        private string GetPostBody(IContent post)
        {
            var body = richTextRenderer.GetRenderedMarkup(post);
            if (body.IsNullOrWhiteSpace())
            {
                body = articulateMarkdownConverter.ToHtml(post.GetValue<string>("markdown") ?? string.Empty);
            }

            return body;
        }

        private static DateTime GetPublishedDate(IContent post)
        {
            DateTime publishedDate = post.GetValue<DateTime>("publishedDate");
            if (publishedDate == default)
            {
                publishedDate = post.CreateDate;
            }

            return publishedDate;
        }

        private XElement CreatePostElement(
            IContent post,
            string body,
            DateTime publishedDate,
            XNamespace nsContent,
            XNamespace nsDsq,
            XNamespace nsWp)
        {
            return new XElement(
                "item",
                new XElement("title", post.Name),
                new XElement("link", publishedUrlProvider.GetUrl(post.Id, UrlMode.Absolute)),
                new XElement(nsContent + "encoded", new XCData(body)),
                new XElement(nsDsq + "thread_identifier", post.Key.ToString()),
                new XElement(nsWp + "post_date_gmt", FormatAsDisqusDate(publishedDate)),
                new XElement(nsWp + "comment_status", "open"));
        }

        private XElement CreateCommentElement(BlogMLComment comment, XNamespace nsWp)
        {
            var commentText = DecodeCommentText(comment);

            return new XElement(
                nsWp + "comment",
                new XElement(nsWp + "comment_id", comment.Id),
                new XElement(nsWp + "comment_author", comment.UserName ?? string.Empty),
                new XElement(nsWp + "comment_author_email", comment.UserEmailAddress ?? string.Empty),
                new XElement(
                    nsWp + "comment_author_url",
                    comment.UserUrl is null ? string.Empty : comment.UserUrl.ToString()),
                // BlogML has no notion of IPs or threading; emit the required Disqus nodes with empty defaults.
                new XElement(nsWp + "comment_author_IP", string.Empty),
                new XElement(nsWp + "comment_date_gmt", FormatAsDisqusDate(comment.CreatedOn)),
                new XElement(nsWp + "comment_content", new XCData(commentText)),
                new XElement(
                    nsWp + "comment_approved",
                    comment.ApprovalStatus == BlogMLApprovalStatus.Approved ? 1 : 0),
                new XElement(nsWp + "comment_parent", "0"));
        }

        private string DecodeCommentText(BlogMLComment comment)
        {
            var commentText = comment.Content?.Content ?? string.Empty;

            if (comment.Content?.ContentType == BlogMLContentType.Base64 &&
                !string.IsNullOrEmpty(comment.Content.Content))
            {
                try
                {
                    commentText = Encoding.UTF8.GetString(Convert.FromBase64String(comment.Content.Content));
                }
                catch (FormatException ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to decode Base64 comment content for comment {CommentId}",
                        comment.Id);
                }
            }

            return commentText;
        }

        private static string FormatAsDisqusDate(DateTime date) =>
            date.ToUniversalTime().ToString(DisqusGmtDateFormat, CultureInfo.InvariantCulture);
    }
}
