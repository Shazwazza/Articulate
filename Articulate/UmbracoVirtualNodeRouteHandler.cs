using System.Globalization;
using System.Linq;
using System.Reflection;
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

            umbracoContext.PublishedContentRequest.ConfigureRequest();

            //create the render model
            var renderModel = new RenderModel(umbracoContext.PublishedContentRequest.PublishedContent, umbracoContext.PublishedContentRequest.Culture);

            //assigns the required tokens to the request
            requestContext.RouteData.DataTokens.Add("umbraco", renderModel);
            requestContext.RouteData.DataTokens.Add("umbraco-doc-request", umbracoContext.PublishedContentRequest);
            requestContext.RouteData.DataTokens.Add("umbraco-context", umbracoContext);

            return new MvcHandler(requestContext);
        }

        protected abstract IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext);

        /// <summary>
        /// Allows inheritors to modify the PublishedContentRequest for things like assigning culture, etc...
        /// </summary>
        /// <param name="publishedContentRequest"></param>
        protected virtual void PreparePublishedContentRequest(PublishedContentRequest publishedContentRequest)
        {
            //We're going to use some reflection to get at the PublishedContentRequestEngine.FindDomain() method which will lookup
            // the domain based on the assigned PublishedContent on the PCR, which will take into account the parent/ancestor ids
            // to find the domain.

            var engine = publishedContentRequest.GetPropertyValue("Engine");
            var findDomainMethod = engine.GetType().GetMethod("FindDomain", BindingFlags.Instance |
                                                                            BindingFlags.NonPublic |
                                                                            BindingFlags.Public);
            findDomainMethod.Invoke(engine, null);

            //NOTE: In Umbraco 7.2, when this is fixed: http://issues.umbraco.org/issue/U4-5628, we don't need to do this and 
            // we can just call Prepare(). Currently we cannot use reflection to call Prepare() because it will still launch
            // the content finders even though a content item is already assigned. in 7.2 this is also fixed.
        }
    }
}