using System;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class ArticulateTagsController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var post = new ListModel(model.Content);
            return View(PathHelper.GetThemeViewPath(post, "List"), post);
        }
    }

    public class ArticulateController : ArticulateListController
    {
        public override ActionResult Index(RenderModel model)
        {
            var list = model.Content.Children.FirstOrDefault(x => StringExtensions.InvariantEquals((string) x.DocumentTypeAlias, (string) "ArticulateList"));
            if (list == null)
            {
                throw new InvalidOperationException("An ArticulateList document must exist under the root Articulate document");
            }

            return base.Index(new RenderModel(list));
        }
    }
}