using System.Web.Mvc;
using System.Xml.Linq;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class WlwManifestController : UmbracoController
    {

        //http://msdn.microsoft.com/en-us/library/bb463260.aspx
        //http://msdn.microsoft.com/en-us/library/bb463263.aspx
        //http://msdn.microsoft.com/en-us/library/bb463265.aspx

        public ActionResult Index(int id)
        {
            var node = Umbraco.TypedContent(id);
            if (node == null)
            {
                return new HttpNotFoundResult();
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
                    new XElement(ns + "supportsGetTags", "Yes"),
                    new XElement(ns + "supportsCommentPolicy", "Yes"),
                    new XElement(ns + "supportsSlug", "Yes"),
                    new XElement(ns + "supportsExcerpt", "Yes"),
                    new XElement(ns + "requiresHtmlTitles", "No")));

            //TODO: If we want to suport look-ahead tags box we need to manually allow it! wtf.

                //new XElement("buttons",
                //    new XElement("button",
                //        new XElement("id", "2"),
                //        new XElement("text", "Tags"),
                //        new XElement("contentUrl", new XCData("{blog-homepage-url}api/tagminiview.aspx")))));

            return new XmlResult(new XDocument(rsd));
        }
    }
}