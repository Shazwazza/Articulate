using System;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class ArticulateAuthorController : ListControllerBase
    {
        public ArticulateAuthorController(IGlobalSettings globalSettings, IUmbracoContextAccessor umbracoContextAccessor, ServiceContext services, AppCaches appCaches, IProfilingLogger profilingLogger, UmbracoHelper umbracoHelper) : base(globalSettings, umbracoContextAccessor, services, appCaches, profilingLogger, umbracoHelper)
        {
        }

        /// <summary>
        /// Override and declare a NonAction so that we get routed to the Index action with the optional page route
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [NonAction]
        public override ActionResult Index(ContentModel model)
        {
            return Index(model, 0);
        }

        public ActionResult Index(ContentModel model, int? p)
        {
            //create a master model
            var masterModel = new MasterModel(model.Content);

            var listNodes = masterModel.RootBlogNode.Children(string.Empty, "ArticulateArchive").ToArray();
            if (listNodes.Length == 0)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            var totalPosts = Umbraco.GetPostCount(model.Content.Name, listNodes.Select(x => x.Id).ToArray());

            if (!GetPagerModel(masterModel, totalPosts, p, out var pager))
            {
                return new RedirectToUmbracoPageResult(model.Content.Parent, UmbracoContextAccessor);
            }

            var authorPosts = Umbraco.GetContentByAuthor(listNodes, model.Content.Name, pager);
            var author = new AuthorModel(model.Content, authorPosts, pager, totalPosts);
            
            return View(PathHelper.GetThemeViewPath(author, "Author"), author);
        }

        
    }
}