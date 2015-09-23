using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Umbraco.Core.Configuration;
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
            //TODO: Instead of using this we can use the native route handlers in umbraco 7.2+
            var ensurePcr = new EnsurePublishedContentRequestAttribute(umbracoContext, "__virtualnodefinder__");

            var found = FindContent(requestContext, umbracoContext);
            if (found == null) return new NotFoundHandler();
            
            //assign the node to our special token
            requestContext.RouteData.DataTokens["__virtualnodefinder__"] = found;
            
            //this hack creates and assigns the pcr to the context - from 7.2+ this also calls Prepare() to 
            // wire up everything in the request
            ensurePcr.OnActionExecuted(new ActionExecutedContext{RequestContext = requestContext});

            //allows inheritors to change the pcr - obsolete though!
            PreparePublishedContentRequest(umbracoContext.PublishedContentRequest);

            //This doesn't execute for less than 7.2.0
            if (UmbracoVersion.Current < Version.Parse("7.2.0"))
            {
                umbracoContext.PublishedContentRequest.ConfigureRequest();   
            }            

            //create the render model
            var renderModel = new RenderModel(umbracoContext.PublishedContentRequest.PublishedContent, umbracoContext.PublishedContentRequest.Culture);

            //assigns the required tokens to the request
            requestContext.RouteData.DataTokens.Add("umbraco", renderModel);
            requestContext.RouteData.DataTokens.Add("umbraco-doc-request", umbracoContext.PublishedContentRequest);
            requestContext.RouteData.DataTokens.Add("umbraco-context", umbracoContext);

            //Here we need to detect if a SurfaceController has posted
            var formInfo = GetFormInfo(requestContext);
            if (formInfo != null)
            {
                //TODO: We are using reflection for this but with the issue http://issues.umbraco.org/issue/U4-5710 fixed we 
                // probably won't need to use our own custom router

                //in order to allow a SurfaceController to work properly, the correct data token needs to be set, so we need to 
                // add a custom RouteDefinition to the collection
                var handle = Activator.CreateInstance("umbraco", "Umbraco.Web.Mvc.RouteDefinition", false, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, null, null, null);
                var def = handle.Unwrap();
                
                def.SetPropertyValue("PublishedContentRequest", umbracoContext.PublishedContentRequest);
                def.SetPropertyValue("ControllerName", requestContext.RouteData.GetRequiredString("controller"));
                def.SetPropertyValue("ActionName", requestContext.RouteData.GetRequiredString("action"));

                requestContext.RouteData.DataTokens["umbraco-route-def"] = def;

                try
                {
                    //First try to call this method as a static method (since it is a static method in umbraco 7.2)
                    // if that fails then we will call it with a non static instance since that is how it was pre-7.2)
                    return (IHttpHandler)typeof(RenderRouteHandler).CallStaticMethod("HandlePostedValues", requestContext, (object)formInfo);
                }
                catch (TargetException)
                {
                    var rrh = new RenderRouteHandler(ControllerBuilder.Current.GetControllerFactory());
                    return (IHttpHandler)rrh.CallMethod("HandlePostedValues", requestContext, (object)formInfo);
                }
            }

            return new MvcHandler(requestContext);
        }

        protected abstract IPublishedContent FindContent(RequestContext requestContext, UmbracoContext umbracoContext);

        /// <summary>
        /// Allows inheritors to modify the PublishedContentRequest for things like assigning culture, etc...
        /// </summary>
        /// <param name="publishedContentRequest"></param>
        [Obsolete("This method should no longer be used, in Umbraco 7.2.0+ most changes to the PCR in this method will result in a YSOD")]
        protected virtual void PreparePublishedContentRequest(PublishedContentRequest publishedContentRequest)
        {
            //NOTE: In Umbraco 7.2, when this is fixed: http://issues.umbraco.org/issue/U4-5628, we don't need to do this at all because the 
            // call to OnActionExecuted does the whole Prepare() operation for us.
            if (UmbracoVersion.Current < Version.Parse("7.2.0"))
            {
                //We're going to use some reflection to get at the PublishedContentRequestEngine.FindDomain() method which will lookup
                // the domain based on the assigned PublishedContent on the PCR, which will take into account the parent/ancestor ids
                // to find the domain.

                var engine = publishedContentRequest.GetPropertyValue("Engine");
                engine.CallMethod("FindDomain");
            }
        }

        /// <summary>
        /// Check the request to see if a SurfaceController has posted any data via a SurfaceController
        /// </summary>
        /// <param name="requestContext"></param>
        /// <returns></returns>
        /// <remarks>
        /// This uses reflection to call the underlying logic that is done in the Umbraco core, this won't be necessary when this
        /// issue is fixed: http://issues.umbraco.org/issue/U4-5710 since we don't have to use our own route handlers.
        /// </remarks>
        private dynamic GetFormInfo(RequestContext requestContext)
        {
            var result = typeof (RenderRouteHandler).CallStaticMethod("GetFormInfo", requestContext);
            return result;          
        }
    }
}