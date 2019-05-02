using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Summary description for FeedController
    /// </summary>
    public class FeedController : PluginController
    {
        [HttpGet]
        [OutputCache(Duration = 120)]
        public ActionResult RenderGitHub(int id)
        {
            var content = Umbraco.Content(id);
            if (content == null) return HttpNotFound();

            var articulateModel = new MasterModel(content);
            var viewPath = PathHelper.GetThemePartialViewPath(articulateModel, "FeedGitHub");
            
            return Content(RenderViewToString(this, viewPath, null, true));
        }        

        /// <summary>
		/// Renders the partial view to string.
		/// </summary>
		/// <param name="controller">The controller context.</param>
		/// <param name="viewName">Name of the view.</param>
		/// <param name="model">The model.</param>
		/// <param name="isPartial">true if it is a Partial view, otherwise false for a normal view </param>
		/// <returns></returns>
        private static string RenderViewToString(ControllerBase controller, string viewName, object model, bool isPartial = false)
        {
            if (controller.ControllerContext == null)
                throw new ArgumentException("The controller must have an assigned ControllerContext to execute this method.");

            controller.ViewData.Model = model;

            using (var sw = new StringWriter())
            {
                var viewResult = !isPartial
                    ? ViewEngines.Engines.FindView(controller.ControllerContext, viewName, null)
                    : ViewEngines.Engines.FindPartialView(controller.ControllerContext, viewName);
                var viewContext = new ViewContext(controller.ControllerContext, viewResult.View, controller.ViewData, controller.TempData, sw);
                viewResult.View.Render(viewContext, sw);
                viewResult.ViewEngine.ReleaseView(controller.ControllerContext, viewResult.View);
                return sw.GetStringBuilder().ToString();
            }
        }
    }
}
