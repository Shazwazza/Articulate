using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;

namespace Articulate.Controllers
{
    public class WlwManifestController : RenderController
    {
        private readonly UmbracoHelper _umbraco;

        public WlwManifestController(            
            UmbracoHelper umbraco,
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _umbraco = umbraco;
        }

        //http://msdn.microsoft.com/en-us/library/bb463260.aspx
        //http://msdn.microsoft.com/en-us/library/bb463263.aspx
        //http://msdn.microsoft.com/en-us/library/bb463265.aspx
        [HttpGet]
        public ActionResult Index(int id)
        {
            var node = _umbraco.Content(id);
            if (node == null)
            {
                return new NotFoundResult();
            }

            var ns = XNamespace.Get("http://schemas.microsoft.com/wlw/manifest/weblog");

            var rsd = new XElement(ns + "manifest",
                new XElement(ns + "options",
                    new XElement(ns + "clientType", "Metaweblog"),
                    new XElement(ns + "supportsNewCategories", "Yes"),
                    new XElement(ns + "supportsPostAsDraft", "Yes"),
                    new XElement(ns + "supportsCustomDate", "Yes"),
                    new XElement(ns + "supportsCategories", "Yes"),
                    new XElement(ns + "supportsCategoriesInline", "Yes"),
                    new XElement(ns + "supportsMultipleCategories", "Yes"),
                    new XElement(ns + "supportsNewCategoriesInline", "Yes"),
                    new XElement(ns + "supportsKeywords", "Yes"),
                    //NOTE: This setting is undocumented for whatever reason!
                    new XElement(ns + "supportsGetTags", "Yes"),
                    new XElement(ns + "supportsCommentPolicy", "Yes"),
                    new XElement(ns + "supportsSlug", "Yes"),
                    new XElement(ns + "supportsExcerpt", "Yes"),
                    new XElement(ns + "requiresHtmlTitles", "No")));

            return new XmlResult(new XDocument(rsd));
        }
    }
}
