using System;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class ArticulateController : ArticulateListController
    {
        public override ActionResult Index(RenderModel model)
        {
            var list = model.Content.Children.FirstOrDefault(x => x.DocumentTypeAlias.InvariantEquals("ArticulateList"));
            if (list == null)
            {
                throw new InvalidOperationException("An ArticulateList document must exist under the root Articulate document");
            }

            return base.Index(new RenderModel(list));
        }
    }

    public class ArticulateListController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var post = new ListModel(model.Content);
            return View(PathHelper.GetThemeViewPath(post, "List"), post);
        }
    }
}