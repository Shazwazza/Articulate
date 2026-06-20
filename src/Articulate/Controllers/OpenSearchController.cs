#nullable enable
using System.Xml.Linq;
using Articulate.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;

namespace Articulate.Controllers
{
    /// <summary>
    ///     Controller for OpenSearch description.
    /// </summary>
    [ArticulateDynamicRoute]
    public class OpenSearchController(
        IPublishedValueFallback publishedValueFallback,
        UmbracoHelper umbraco,
        ILogger<OpenSearchController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor)
        : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        /// <summary>
        ///     Renders the OpenSearch description XML.
        /// </summary>
        [HttpGet]
        public ActionResult Index(int id)
        {
            IPublishedContent? node = umbraco.Content(id);
            if (node is null)
            {
                return new NotFoundResult();
            }

            var model = new MasterModel(node, publishedValueFallback);
            if (!model.HasSearchRoute())
            {
                return new NotFoundResult();
            }

            var searchTemplateUrl = model.ArticulateSearchUrl(true) + "?term={searchTerms}";

            XNamespace ns = "http://a9.com/-/spec/opensearch/1.1/";

            var rsd = new XElement(
                ns + "OpenSearchDescription",
                new XElement(ns + "ShortName", model.PageTitle),
                new XElement(ns + "Description", model.PageDescription),
                new XElement(ns + "InputEncoding", "UTF-8"),
                new XElement(
                    ns + "Url",
                    new XAttribute("type", "text/html"),
                    new XAttribute("method", "get"),
                    new XAttribute("template", searchTemplateUrl)));

            return new XmlResult(new XDocument(rsd));
        }
    }
}
