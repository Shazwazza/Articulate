using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Editors;

namespace Articulate.Controllers
{
    public class ArticulateBlogDataInstallController : UmbracoAuthorizedJsonController
    {
        private readonly ArticulateDataInstaller _installer;

        public ArticulateBlogDataInstallController(IGlobalSettings globalSettings, IUmbracoContextAccessor umbracoContextAccessor, ISqlContext sqlContext, ServiceContext services, AppCaches appCaches, IProfilingLogger logger, IRuntimeState runtimeState, UmbracoHelper umbracoHelper, ArticulateDataInstaller installer) : base(globalSettings, umbracoContextAccessor, sqlContext, services, appCaches, logger, runtimeState, umbracoHelper)
        {
            _installer = installer;
        }
        
        public IHttpActionResult PostInstall()
        {
            return NotFound();

            ////TODO: indicate that it's already installed and no changes have been made
            //var root = _installer.Execute(out bool packageInstalled);

            //string blogUrl;

            //if (root != null)
            //{
            //    blogUrl = Umbraco.Content(root.Id).Url;
            //}
            //else
            //{
            //    blogUrl = "/";
            //}

            //return Ok(blogUrl);
        }
    }
}