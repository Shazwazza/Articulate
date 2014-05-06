using System;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the Articulate root node as the main blog post list by date
    /// </summary>
    public class ArticulateController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var listNode = model.Content.Children
                .FirstOrDefault(x => x.DocumentTypeAlias.InvariantEquals("ArticulateList"));
            if (listNode == null)
            {
                throw new InvalidOperationException("An ArticulateList document must exist under the root Articulate document");
            }

            var listModel = new ListModel(listNode);
            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }
    }
}