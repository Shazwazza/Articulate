using System;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the Articulate root node as the main blog post list by date
    /// </summary>
    public class ArticulateController : ListControllerBase
    {
        /// <summary>
        /// Declare new Index action with optional page number
        /// </summary>
        /// <param name="model"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public ActionResult Index(ContentModel model, int? p)
        {
            return RenderView(model, p);
        }

        /// <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [NonAction]
        public override ActionResult Index(ContentModel model)
        {
            return RenderView(model);
        }

        private ActionResult RenderView(ContentModel model, int? p = null)
        {
            var listNodes = model.Content.Children("ArticulateArchive").ToArray();
            if (listNodes.Length == 0)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            var master = new MasterModel(model.Content);

            var count = Umbraco.GetPostCount(listNodes.Select(x => x.Id).ToArray());

            var posts = Umbraco.GetRecentPosts(master, p ?? 1, master.PageSize);

            return GetPagedListView(master, listNodes[0], posts, count, p);

        }
    }
}