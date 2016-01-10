using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    [UmbracoAuthorize]
    public class ArticulateBlogImportDataController : SurfaceController
    {
        public FileResult DownloadDisqusExport()
        {
            return null;
        }

        public FileResult DownloadBlogMlExport()
        {
            return null;
        }
    }
}