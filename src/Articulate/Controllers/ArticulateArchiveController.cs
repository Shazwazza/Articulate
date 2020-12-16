using System;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Controllers
{
    /// <summary>
    /// Renders the Articulate Archive node as a blog post list by date
    /// </summary>
    public class ArticulateArchiveController : ListControllerBase
    {
        public ArticulateArchiveController(IGlobalSettings globalSettings, IUmbracoContextAccessor umbracoContextAccessor, ServiceContext services, AppCaches appCaches, IProfilingLogger profilingLogger, UmbracoHelper umbracoHelper)
            : base(globalSettings, umbracoContextAccessor, services, appCaches, profilingLogger, umbracoHelper)
        {
        }

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

        private ActionResult RenderView(IContentModel model, int? p = null)
        {
            var archive = new MasterModel(model.Content);

            // redirect to root node when "redirectArchive" is configured
            if (archive.RootBlogNode.Value<bool>("redirectArchive"))
            {
                return RedirectPermanent(archive.RootBlogNode.Url);
            }

            //Get post count by xpath is much faster than iterating all children to get a count
            var count = Umbraco.GetPostCount(archive.Id);

            int pageSize;
            if(!Int32.TryParse(archive.RootBlogNode.Value<string>("pageSize"), out pageSize))
            {
                pageSize = 10;
            }

            var posts = Umbraco.GetRecentPostsByArchive(archive, 1, pageSize);

            return GetPagedListView(archive, archive, posts, count, null);          
        }
    }
}
