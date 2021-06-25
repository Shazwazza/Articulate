using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    /// <summary>
    /// Really simple discovery controller
    /// </summary>
    public class RsdController : Controller
    {
        private readonly UmbracoHelper _umbracoHelper;

        public RsdController(UmbracoHelper umbracoHelper) => _umbracoHelper = umbracoHelper;

        /// <summary>
        /// Renders the RSD for the articulate node id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Index(int id)
        {
            var node = _umbracoHelper.Content(id);
            if (node == null)
            {
                return new NotFoundResult();
            }

            var rsd = new XElement("rsd",
                new XAttribute("version", "1.0"),
                new XElement("service",
                    new XElement("engineName", "Articulate, powered by Umbraco"),
                    new XElement("engineLink", "http://github.com/shandem/articulate"),
                    new XElement("homePageLink", node.Url(mode: UrlMode.Absolute))),
                new XElement("apis",
                    new XElement("api",
                        new XAttribute("name", "MetaWeblog"),
                        new XAttribute("preferred", true),
                        new XAttribute("apiLink", node.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "metaweblog/" + id),
                        new XAttribute("blogID", node.Url(mode: UrlMode.Absolute)))));

            return new XmlResult(new XDocument(rsd));
        }
    }

    internal class XmlResult : ActionResult
    {
        private readonly XDocument _xDocument;

        public XmlResult(XDocument xDocument) => _xDocument = xDocument;

        /// <summary>
        /// Serialises the object that was passed into the constructor to XML and writes the corresponding XML to the result stream.
        /// </summary>
        public override async Task ExecuteResultAsync(ActionContext context)
        {
            if (_xDocument == null)
            {
                return;
            }

            context.HttpContext.Response.Clear();
            context.HttpContext.Response.ContentType = "text/xml";
            await context.HttpContext.Response.WriteAsync(_xDocument.ToString());
        }
    }
}
