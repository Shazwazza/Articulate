using System;
using System.Web;
using System.Web.Mvc;
using Umbraco.Web;

namespace Articulate.Controllers
{
    /// <summary>
    /// A custom action result that wraps PublishedContentNotFoundHandler
    /// </summary>
    internal class UmbracoNotFoundResult : HttpNotFoundResult
    {
        public override void ExecuteResult(ControllerContext context)
        {
            base.ExecuteResult(context);

            //pretty hacky since this is internal in Umbraco right now
            var umbNotFoundHandler = (IHttpHandler)Activator.CreateInstance(
                typeof(UmbracoContext).Assembly
                    .GetType("Umbraco.Web.Routing.PublishedContentNotFoundHandler", true), 
                "In addition, no template exists to render the custom 404.");

            //Hack - we need the real HttpContext not an HttpContextBase to use an IHttpHandler
            umbNotFoundHandler.ProcessRequest(HttpContext.Current);
        }
    }
}