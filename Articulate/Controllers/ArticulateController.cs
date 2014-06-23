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
        /// <summary>
        /// Declare new Index action with optional page number
        /// </summary>
        /// <param name="model"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public ActionResult Index(RenderModel model, int? p)
        {
            return RenderView(model, p);
        }

        /// <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [NonAction]
        public override ActionResult Index(RenderModel model)
        {
            return RenderView(model);
        }

        private ActionResult RenderView(IRenderModel model, int? p = null)
        {
            var listNode = model.Content.Children
               .FirstOrDefault(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive"));
            if (listNode == null)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            if (p == null || p.Value <= 0)
            {
                p = 1;
            }

            var rootPageModel = new ListModel(model.Content);

            //TODO: I wonder about the performance of this - when we end up with thousands of blog posts, 
            // this will probably not be so efficient. I wonder if using an XPath lookup for batches of children
            // would work? The children count could be cached. I'd rather not put blog posts under 'month' nodes
            // just for the sake of performance. Hrm.... Examine possibly too.

            var totalPosts = listNode.Children.Count();
            var pageSize = rootPageModel.PageSize;
            var totalPages = Convert.ToInt32(Math.Ceiling((double)totalPosts/pageSize));

            //Invalid page, redirect without pages
            if (totalPages > 0 && totalPages < p)
            {
                return new RedirectToUmbracoPageResult(model.Content, UmbracoContext);
            }

            var pager = new PagerModel(
                pageSize,
                p.Value - 1,
                totalPages,
                totalPages > p ? model.Content.Url.EnsureEndsWith('?') + "p=" + (p + 1) : null,
                p > 1 ? model.Content.Url.EnsureEndsWith('?') + "p=" + (p - 1) : null);

            var listModel = new ListModel(listNode, pager);
            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }
    }
}