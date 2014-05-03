using System;
using System.Linq;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Web.Models;

namespace Articulate.Controllers
{
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