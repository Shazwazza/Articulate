using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Web.BackOffice.Controllers;

namespace Articulate.Controllers
{
    public class ArticulatePropertyEditorsController : UmbracoAuthorizedApiController
    {
        private readonly IHostingEnvironment _hostingEnvironment;

        public ArticulatePropertyEditorsController(IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public IEnumerable<string> GetThemes()
        {
            var dir = _hostingEnvironment.MapPathWebRoot(PathHelper.VirtualThemePath);
            return Directory.GetDirectories(dir).Select(x => new DirectoryInfo(x).Name);
        }
    }
}
