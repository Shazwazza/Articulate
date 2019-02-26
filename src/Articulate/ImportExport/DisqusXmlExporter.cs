using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Argotic.Syndication.Specialized;
using HeyRed.MarkdownSharp;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Articulate.ImportExport
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class DisqusXmlExporter
    {
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public DisqusXmlExporter(IUmbracoContextAccessor umbracoContextAccessor)
        {
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        public XDocument Export(IEnumerable<IContent> posts, BlogMLDocument document)
        {
            var nsContent = XNamespace.Get("http://purl.org/rss/1.0/modules/content/");
            var nsDsq = XNamespace.Get("http://www.disqus.com/");
            var nsDc = XNamespace.Get("http://purl.org/dc/elements/1.1/");
            var nsWp = XNamespace.Get("http://wordpress.org/export/1.0/");

            var xChannel = new XElement("channel");

            var xDoc = new XDocument(
                new XElement("rss",
                    new XAttribute("version", "2.0"),
                    new XAttribute(XNamespace.Xmlns + "content", nsContent),
                    new XAttribute(XNamespace.Xmlns + "dsq", nsDsq),
                    new XAttribute(XNamespace.Xmlns + "dc", nsDc),
                    new XAttribute(XNamespace.Xmlns + "wp", nsWp),
                    xChannel));
            
            foreach (var post in posts)
            {
                var blogMlPost = document.Posts.FirstOrDefault(x => x.Title.Content == post.Name);

                //TODO: Add logging here if we cant find it
                if (blogMlPost == null) continue;

                //no comments to import
                if (blogMlPost.Comments.Any() == false) continue;

                var body = post.GetValue<string>("richText");
                if (body.IsNullOrWhiteSpace())
                {
                    var md = new Markdown();
                    body = md.Transform(post.GetValue<string>("markdown"));
                }

                var xItem = new XElement("item",
                    new XElement("title", post.Name),
                    new XElement("link", _umbracoContextAccessor?.UmbracoContext?.UrlAbsolute(post.Id) ?? string.Empty),
                    new XElement(nsContent + "encoded", new XCData(body)),
                    new XElement(nsDsq + "thread_identifier", post.Key.ToString()),
                    new XElement(nsWp + "post_date_gmt", post.GetValue<DateTime>("publishedDate").ToUniversalTime().ToIsoString()),
                    new XElement(nsWp + "comment_status", "open"));

                foreach (var comment in blogMlPost.Comments)
                {
                    string commentText = comment.Content.Content;

                    if (comment.Content.ContentType == BlogMLContentType.Base64)
                        commentText = Encoding.UTF8.GetString(Convert.FromBase64String(comment.Content.Content));

                    var xComment = new XElement(nsWp + "comment",
                        new XElement(nsWp + "comment_id", comment.Id),
                        new XElement(nsWp + "comment_author", comment.UserName),
                        new XElement(nsWp + "comment_author_email", comment.UserEmailAddress),
                        new XElement(nsWp + "comment_author_url", comment.UserUrl == null ? string.Empty : comment.UserUrl.ToString()),
                        new XElement(nsWp + "comment_date_gmt", comment.CreatedOn.ToUniversalTime().ToIsoString()),
                        new XElement(nsWp + "comment_content", commentText),
                        new XElement(nsWp + "comment_approved", comment.ApprovalStatus == BlogMLApprovalStatus.Approved ? 1 : 0));

                    xItem.Add(xComment);
                }

                xChannel.Add(xItem);
            }

            return xDoc;
        }
    }
}