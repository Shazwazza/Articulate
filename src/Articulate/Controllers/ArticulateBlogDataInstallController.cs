using Articulate.Packaging;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.BackOffice.Controllers;

namespace Articulate.Controllers
{
    public class ArticulateBlogDataInstallController : UmbracoAuthorizedJsonController
    {
        private readonly ArticulateDataInstaller _installer;
        private readonly IBackOfficeSecurityAccessor _security;

        public ArticulateBlogDataInstallController(
            ArticulateDataInstaller installer,
            IBackOfficeSecurityAccessor security)
        {
            _installer = installer;
            _security = security;
        }

        public IActionResult PostInstall()
        {
            var dataInstalled = _installer.InstallSchemaAndContent(_security.BackOfficeSecurity.GetUserId().ResultOr(-1));

            return Ok(dataInstalled);
        }
    }
}
