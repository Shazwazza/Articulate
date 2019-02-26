using System.Web.Mvc;
using System.Xml.Linq;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class OpenSearchController : PluginController
    {
        [HttpGet]
        public ActionResult Index(int id)
        {
            //TODO: Seems hanslemans is slightly wrong compared to the other ones found on the interwebs

            //<opensearchdescription xmlns="http://a9.com/-/spec/opensearch/1.1/">
            //  <shortname>Hanselman Search</shortname>
            //  <description>Search Scott Hanselman's Blog</description>
            //  <url type="text/html" method="get" template="http://www.hanselman.com/blog?q={searchTerms}">
            //  <img width="16" height="16">http://www.hanselman.com/blog/favicon.ico
            //  <inputencoding>UTF-8</inputencoding>
            //  <searchform>http://www.hanselman.com/</searchform>
            //</url></opensearchdescription>

            //<?xml version="1.0" encoding="UTF-8" ?>
            //<OpenSearchDescription xmlns="http://a9.com/-/spec/opensearch/1.1/" xmlns:moz="http://www.mozilla.org/2006/browser/search/">
            //  <ShortName>CodeClimber</ShortName>
            //  <Description>Search CodeClimber</Description>
            //  <InputEncoding>UTF-8</InputEncoding>
            //  <Image width="16" height="16" type="image/x-icon">http://codeclimber.net.nz/App_Plugins/Articulate/Themes/PhantomV2/assets/img/favicon.ico</Image>
            //  <Url type="text/html" method="get" template="http://codeclimber.net.com/search?term={searchTerms}"></Url>
            //</OpenSearchDescription>

            //<?xml version="1.0" encoding="UTF-8"?>
            //<OpenSearchDescription xmlns:moz="http://www.mozilla.org/2006/browser/search/" 
            //      xmlns="http://a9.com/-/spec/opensearch/1.1/">
            //  <ShortName>aaron.pk</ShortName>
            //  <Description>Search aaron.pk</Description>
            //  <InputEncoding>UTF-8</InputEncoding>
            //  <Url method="get" type="text/html" 
            //      template="http://aaron.pk/search?q={searchTerms}"/>
            //</OpenSearchDescription>

            var node = Umbraco.Content(id);
            if (node == null)
            {
                return new HttpNotFoundResult();
            }

            var model = new MasterModel(node);
            
            var searchTemplateUrl = Url.ArticulateSearchUrl(model, includeDomain:true) + "?term={searchTerms}";

            XNamespace ns = "http://a9.com/-/spec/opensearch/1.1/";

            var rsd = new XElement(ns + "OpenSearchDescription",
                new XElement(ns + "ShortName", model.PageTitle),
                new XElement(ns + "Description", model.PageDescription),
                new XElement(ns + "InputEncoding", "UTF-8"),
                new XElement(ns + "Url",
                    new XAttribute("type", "text/html"),
                    new XAttribute("method", "get"),
                    new XAttribute("template", searchTemplateUrl)));
            
            return new XmlResult(new XDocument(rsd));
        }
    }
}