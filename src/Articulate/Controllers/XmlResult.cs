#nullable enable
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Articulate.Controllers
{
    internal class XmlResult(XDocument xDocument) : ActionResult
    {
        /// <summary>
        ///     Serialises the object that was passed into the constructor to XML and writes the corresponding XML to the result
        ///     stream.
        /// </summary>
        public override Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.Clear();
            context.HttpContext.Response.ContentType = "text/xml";
            return context.HttpContext.Response.WriteAsync(xDocument.ToString());
        }
    }
}
