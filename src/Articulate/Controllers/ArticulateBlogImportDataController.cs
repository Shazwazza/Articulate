using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Umbraco.Web.Editors;
using Umbraco.Web.WebApi;

namespace Articulate.Controllers
{    
    public class ArticulateBlogDataInstallController : UmbracoAuthorizedJsonController
    {
        public IHttpActionResult PostInstall()
        {
            var dataInstaller = new ArticulateDataInstaller(Services, Security.CurrentUser.Id);
            var root = dataInstaller.Execute();

            string blogUrl;

            if (root != null)
            {
                blogUrl = Umbraco.TypedContent(root.Id).Url;
            }
            else
            {
                blogUrl = "/";
            }

            return Ok(blogUrl);
        }
    }
}