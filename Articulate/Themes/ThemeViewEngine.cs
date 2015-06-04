using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.Core.IO;
using Umbraco.Core;
using Umbraco.Web.Mvc;
using Umbraco.Web.Models;

namespace Articulate.Themes
{
    /// <summary>
    /// A view engine to look into the App_Plugins folder for views for packaged controllers
    /// </summary>
    public class ThemeViewEngine : ReflectedFixedRazorViewEngine
    {

        /// <summary>
        /// Constructor
        /// </summary>
        private readonly IEnumerable<string> _viewLocations = new[] { "/{0}.cshtml" };
		private readonly IEnumerable<string> _partialViewLocations = new[] { "/Partials/{0}.cshtml", "/{0}.cshtml" };

		/// <summary>
		/// Constructor
		/// </summary>
        public ThemeViewEngine() : base()
		{
			string templateFolder = "~/Themes/%1/Views";

            var replaceWithThemeViewFolder = _viewLocations.ForEach(location => templateFolder + location);
			var replaceWithThemePartialViewFolder = _partialViewLocations.ForEach(location => templateFolder + location);

			//The Render view engine doesn't support Area's so make those blank
            ViewLocationFormats = replaceWithThemeViewFolder.ToArray();
			PartialViewLocationFormats = replaceWithThemePartialViewFolder.ToArray();

			AreaPartialViewLocationFormats = new string[] { };
			AreaViewLocationFormats = new string[] { };

		}

        public override ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache)
        {

            if (!ShouldFindView(controllerContext, false))
            {
                return new ViewEngineResult(new string[] { });
            }

            if (controllerContext.HttpContext.Items["theme"] != null)
            {
                string theme = controllerContext.HttpContext.Items["theme"].ToString();
                ViewLocationFormats = ViewLocationFormats.ForEach(location => location.Replace("%1", theme)).ToArray();
            }
            var result = base.FindView(controllerContext, viewName, masterName, useCache);
            return result;
        }

        public override ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName, bool useCache)
        {
            if (!ShouldFindView(controllerContext, false))
            {
                return new ViewEngineResult(new string[] { });
            }
            if (controllerContext.HttpContext.Items["theme"] != null)
            {
                string theme = controllerContext.HttpContext.Items["theme"].ToString();
                PartialViewLocationFormats = PartialViewLocationFormats.ForEach(location => location.Replace("%1", theme)).ToArray();
            }
            var result = base.FindPartialView(controllerContext, partialViewName, useCache);
            return result;
        }

        /// <summary>
        /// Determines if the view should be found, this is used for view lookup performance and also to ensure 
        /// less overlap with other user's view engines. This will return true if the Umbraco back office is rendering
        /// and its a partial view or if the umbraco front-end is rendering but nothing else.
        /// </summary>
        /// <param name="controllerContext"></param>
        /// <param name="isPartial"></param>
        /// <returns></returns>
        private bool ShouldFindView(ControllerContext controllerContext, bool isPartial)
        {
            var umbracoToken = GetDataTokenInViewContextHierarchy(controllerContext, "umbraco");

            //first check if we're rendering a partial view for the back office, or surface controller, etc...
            //anything that is not IUmbracoRenderModel as this should only pertain to Umbraco views.
            if (isPartial && ((umbracoToken is RenderModel) == false))
            {
                return true;
            }

            //only find views if we're rendering the umbraco front end
            if (umbracoToken is RenderModel)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// This function already exists in Umbraco as an extension method, but it is internal :(
        /// Maybe it can be made public?
        /// </summary>
        private object GetDataTokenInViewContextHierarchy(ControllerContext controllerContext, string dataTokenName)
        {
            if (controllerContext.RouteData.DataTokens.ContainsKey(dataTokenName))
            {
                return controllerContext.RouteData.DataTokens[dataTokenName];
            }

            if (controllerContext.ParentActionViewContext != null)
            {
                //recurse!
                return GetDataTokenInViewContextHierarchy(controllerContext.ParentActionViewContext, dataTokenName);
            }

            return null;
        }
    }
}
