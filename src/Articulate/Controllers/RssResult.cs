#nullable enable
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Controllers
{
    internal class RssResult(SyndicationFeed feed, IMasterModel model) : ActionResult
    {
        /// <inheritdoc />
        public override async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.ContentType = "application/xml";

            await using var txtWriter = new Utf8StringWriter();
            await using var xmlWriter = XmlWriter.Create(
                txtWriter,
                new XmlWriterSettings { Indent = true, Async = true, Encoding = Encoding.UTF8 });

            // Write the Processing Instruction node.
            var xsltHeader =
                $"type=\"text/xsl\" href=\"{model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "rss/xslt"}\"";
            await xmlWriter.WriteProcessingInstructionAsync("xml-stylesheet", xsltHeader);

            Rss20FeedFormatter formatter = feed.GetRss20Formatter();
            formatter.WriteTo(xmlWriter);

            await xmlWriter.FlushAsync();

            await context.HttpContext.Response.WriteAsync(txtWriter.ToString());
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
