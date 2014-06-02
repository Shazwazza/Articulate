using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbraco.Core.IO;
using Umbraco.Web.WebApi;

namespace Articulate.Controllers
{
    public class ArticulatePropertyEditorsController : UmbracoApiController
    {
        public IEnumerable<string> GetThemes()
        {
            var dir = IOHelper.MapPath("~/App_Plugins/Articulate/Themes");
            return Directory.GetDirectories(dir).Select(x => new DirectoryInfo(x).Name);
        }
    }
}