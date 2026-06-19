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
    ///     Controller for Windows Live Writer manifest.
    /// </summary>
    [ArticulateDynamicRoute]
    public class WlwManifestController(
        UmbracoHelper umbraco,
        ILogger<WlwManifestController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor)
        : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        // http://msdn.microsoft.com/en-us/library/bb463260.aspx
        // http://msdn.microsoft.com/en-us/library/bb463263.aspx
        // http://msdn.microsoft.com/en-us/library/bb463265.aspx
        /// <summary>
        ///     Renders the Windows Live Writer manifest XML.
        /// </summary>
        [HttpGet]
        public ActionResult Index(int id)
        {
            IPublishedContent? node = umbraco.Content(id);
            if (node is null)
            {
                return new NotFoundResult();
            }

            var ns = XNamespace.Get("http://schemas.microsoft.com/wlw/manifest/weblog");

            var rsd = new XElement(
                ns + "manifest",
                new XElement(
                    ns + "options",
                    new XElement(ns + "clientType", "Metaweblog"),
                    new XElement(ns + "supportsNewCategories", "Yes"),
                    new XElement(ns + "supportsPostAsDraft", "Yes"),
                    new XElement(ns + "supportsCustomDate", "Yes"),
                    new XElement(ns + "supportsCategories", "Yes"),
                    new XElement(ns + "supportsCategoriesInline", "Yes"),
                    new XElement(ns + "supportsMultipleCategories", "Yes"),
                    new XElement(ns + "supportsNewCategoriesInline", "Yes"),
                    new XElement(ns + "supportsKeywords", "Yes"),

                    // NOTE: This setting is undocumented for whatever reason!
                    new XElement(ns + "supportsGetTags", "Yes"),
                    new XElement(ns + "supportsCommentPolicy", "Yes"),
                    new XElement(ns + "supportsSlug", "Yes"),
                    new XElement(ns + "supportsExcerpt", "Yes"),
                    new XElement(ns + "requiresHtmlTitles", "No")));

            return new XmlResult(new XDocument(rsd));
        }
    }
}
