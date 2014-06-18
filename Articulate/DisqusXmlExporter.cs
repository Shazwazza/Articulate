using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Argotic.Syndication.Specialized;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Articulate
{
    internal class DisqusXmlExporter
    {
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

            var umbHelper = new UmbracoHelper(UmbracoContext.Current);
            var markDown = new MarkdownDeep.Markdown();

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
                    body = markDown.Transform(post.GetValue<string>("markdown"));
                }
                
                var xItem = new XElement("item",
                    new XElement("title", post.Name),
                    new XElement("link", umbHelper.NiceUrlWithDomain(post.Id)),
                    new XElement(nsContent + "encoded", new XCData(body)),
                    new XElement(nsDsq + "thread_identifier", post.Id.ToString(CultureInfo.InvariantCulture)),
                    new XElement(nsWp + "post_date_gmt", post.GetValue<DateTime>("publishedDate").ToUniversalTime().ToIsoString()),
                    new XElement(nsWp + "comment_status", "open"));

                foreach (var comment in blogMlPost.Comments)
                {
                    var xComment = new XElement(nsWp + "comment",
                        new XElement(nsWp + "comment_id", comment.Id),
                        new XElement(nsWp + "comment_author", comment.UserName),
                        new XElement(nsWp + "comment_author_email", comment.UserEmailAddress),
                        new XElement(nsWp + "comment_author_url", comment.UserUrl == null ? string.Empty : comment.UserUrl.ToString()),
                        new XElement(nsWp + "comment_date_gmt", comment.CreatedOn.ToUniversalTime().ToIsoString()),
                        new XElement(nsWp + "comment_content", comment.Content.Content),
                        new XElement(nsWp + "comment_approved", comment.ApprovalStatus == BlogMLApprovalStatus.Approved ? 1 : 0));

                    xItem.Add(xComment);
                }

                xChannel.Add(xItem);
            }

            return xDoc;
        }
    }
}