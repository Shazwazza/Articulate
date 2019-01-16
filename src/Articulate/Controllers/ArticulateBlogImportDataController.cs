using System.Web.Http;
using Umbraco.Web.Editors;

namespace Articulate.Controllers
{
    public class ArticulateBlogDataInstallController : UmbracoAuthorizedJsonController
    {
        public IHttpActionResult PostInstall()
        {
            var dataInstaller = new ArticulateDataInstaller(Security.CurrentUser.Id);

            //TODO: indicate that it's already installed and no changes have been made
            var root = dataInstaller.Execute(out bool packageInstalled);

            string blogUrl;

            if (root != null)
            {
                blogUrl = Umbraco.Content(root.Id).Url;
            }
            else
            {
                blogUrl = "/";
            }

            return Ok(blogUrl);
        }
    }
}