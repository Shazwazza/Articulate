using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Linq;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Really simple discovery controller
    /// </summary>
    public class RsdController : UmbracoController
    {
        /// <summary>
        /// Renders the RSD for the articulate node id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Index(int id)
        {
            var node = Umbraco.TypedContent(id);
            if (node == null)
            {
                return new HttpNotFoundResult();
            }

            var rsd = new XElement("rsd",
                new XAttribute("version", "1.0"),
                new XElement("service",
                    new XElement("engineName", "Articulate, powered by Umbraco"),
                    new XElement("engineLink", "http://github.com/shandem/articulate"),
                    new XElement("homePageLink", node.UrlWithDomain())),
                new XElement("apis",
                    new XElement("api",
                        new XAttribute("name", "MetaWeblog"),
                        new XAttribute("preferred", true),
                        new XAttribute("apiLink", node.UrlWithDomain().EnsureEndsWith('/') + "metaweblog/" + id),
                        new XAttribute("blogID", node.UrlWithDomain()))));

            return new XmlResult(new XDocument(rsd));
        }
    }

    internal class XmlResult : ActionResult
    {
        private readonly XDocument _xDocument;

        public XmlResult(XDocument xDocument)
        {
            _xDocument = xDocument;
        }

        /// <summary>
        /// Serialises the object that was passed into the constructor to XML and writes the corresponding XML to the result stream.
        /// </summary>
        /// <param name="context">The controller context for the current request.</param>
        public override void ExecuteResult(ControllerContext context)
        {
            if (_xDocument != null)
            {
                context.HttpContext.Response.Clear();
                context.HttpContext.Response.ContentType = "text/xml";
                context.HttpContext.Response.Output.Write(_xDocument.ToString());
            }
        }
    }
}