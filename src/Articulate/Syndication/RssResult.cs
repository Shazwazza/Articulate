using System.IO;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Articulate.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Articulate.Syndication
{
    public class RssResult : ActionResult
    {
        private readonly SyndicationFeed _feed;
        private readonly IMasterModel _model;

        public RssResult(SyndicationFeed feed, IMasterModel model)
        {
            _feed = feed;
            _model = model;
        }


        public override async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.ContentType = "application/xml";

            using (var txtWriter = new Utf8StringWriter())
            {
                var xmlWriter = XmlWriter.Create(txtWriter, new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    OmitXmlDeclaration = false
                });

                // Write the Processing Instruction node.
                var xsltHeader = string.Format("type=\"text/xsl\" href=\"{0}\"", _model.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "rss/xslt");
                xmlWriter.WriteProcessingInstruction("xml-stylesheet", xsltHeader);

                var formatter = _feed.GetRss20Formatter();
                formatter.WriteTo(xmlWriter);

                xmlWriter.Flush();

                await context.HttpContext.Response.WriteAsync(txtWriter.ToString());
            }
        }

        public sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }
        }
    }
}
