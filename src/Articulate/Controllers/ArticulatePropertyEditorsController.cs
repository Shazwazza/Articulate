using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Web.BackOffice.Controllers;

namespace Articulate.Controllers
{
    public class ArticulatePropertyEditorsController : UmbracoAuthorizedApiController
    {
        private readonly IHostEnvironment _hostingEnvironment;

        public ArticulatePropertyEditorsController(IHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public IEnumerable<string> GetThemes()
        {
            var defaultThemeDir = _hostingEnvironment.MapPathContentRoot(PathHelper.VirtualThemePath);
            var defaultThemes = Directory.GetDirectories(defaultThemeDir).Select(x => new DirectoryInfo(x).Name);

            var userThemeDir = _hostingEnvironment.MapPathContentRoot(PathHelper.UserVirtualThemePath);
            var userThemes = Directory.GetDirectories(userThemeDir).Select(x => new DirectoryInfo(x).Name);

            return userThemes.Union(defaultThemes);
        }
    }
}
