using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;
using Umbraco.Web.Routing;

namespace Articulate
{
    public abstract class UmbracoVirtualNodeRouteHandler : IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            var umbracoContext = UmbracoContext.Current;

            //TODO: This is a huge hack - we need to publicize some stuff in the core
            //TODO: publicize: ctor (or static method to create it), Prepared()
            var ensurePcr = new EnsurePublishedContentRequestAttribute(umbracoContext, "__virtualnodefinder__");

            var found = FindContent(requestContext, umbracoContext);
            if (found == null) return new NotFoundHandler();
            
            //assign the node to our special token
            requestContext.RouteData.DataTokens["__virtualnodefinder__"] = found;
            
            //this hack creates and assigns the pcr to the context
            ensurePcr.OnActionExecuted(new ActionExecutedContext{RequestContext = requestContext});

            //allows inheritors to change the pcr
            PreparePublishedContentRequest(umbracoContext.PublishedContentRequest);

            //create the render model
            var renderModel = new RenderModel(umbracoContext.PublishedContentRequest.PublishedContent, umbracoContext.PublishedContentRequest.Culture);

            //assigns the required tokens to the request
            requestContext.RouteData.DataTokens.Add("umbraco", renderModel);
            requestContext.RouteData.DataTokens.Add("umbraco-doc-request", umbracoContext.PublishedContentRequest);
            requestContext.RouteData.DataTokens.Add("umbraco-context", umbracoContext);

            umbracoContext.PublishedContentRequest.ConfigureRequest();

            return new MvcHandler(requestContext);
        }

        protected abstract IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext);

        /// <summary>
        /// Allows inheritors to modify the PublishedContentRequest for things like assigning culture, etc...
        /// </summary>
        /// <param name="publishedContentRequest"></param>
        protected virtual void PreparePublishedContentRequest(PublishedContentRequest publishedContentRequest)
        {
        }
    }
}