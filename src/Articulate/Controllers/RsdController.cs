#nullable enable
using System.Xml.Linq;
using Articulate.Attributes;
using Articulate.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;

namespace Articulate.Controllers
{
    /// <summary>
    /// Really simple discovery controller
    /// </summary>
    [ArticulateDynamicRoute]
    public class RsdController(
        UmbracoHelper umbracoHelper,
        ILogger<RsdController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IOptions<ArticulateOptions> articulateOptions)
        : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        /// <summary>
        /// Renders the RSD for the articulate node id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Index(int id)
        {
            if (!articulateOptions.Value.EnableMetaWeblog)
            {
                return new NotFoundResult();
            }

            IPublishedContent? node = umbracoHelper.Content(id);
            if (node is null)
            {
                return new NotFoundResult();
            }

            var rsd = new XElement(
                "rsd",
                new XAttribute("version", "1.0"),
                new XElement(
                    "service",
                    new XElement("engineName", "Articulate, powered by Umbraco"),
                    new XElement("engineLink", "https://github.com/shazwazza/articulate"),
                    new XElement("homePageLink", node.Url(mode: UrlMode.Absolute))),
                new XElement(
                    "apis",
                    new XElement(
                        "api",
                        new XAttribute("name", "MetaWeblog"),
                        new XAttribute("preferred", true),
                        new XAttribute("apiLink", node.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "metaweblog/" + id),
                        new XAttribute("blogID", node.Url(mode: UrlMode.Absolute)))));

            return new XmlResult(new XDocument(rsd));
        }
    }
}
