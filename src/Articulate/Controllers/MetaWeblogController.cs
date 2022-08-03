using System.IO;
using System.Text;
using System.Threading.Tasks;
using Articulate.MetaWeblog;
using Examine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using WilderMinds.MetaWeblog;

namespace Articulate.Controllers
{
    public class MetaWeblogController : RenderController
    {
        private readonly ArticulateMetaWeblogProvider _articulateMetaWeblogProvider;
        private readonly MetaWeblogService _metaWeblogService;

        public MetaWeblogController(ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            ArticulateMetaWeblogProvider articulateMetaWeblogProvider,
            MetaWeblogService metaWeblogService)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _articulateMetaWeblogProvider = articulateMetaWeblogProvider;
            _metaWeblogService = metaWeblogService;
        }

        [HttpPost]
        public async Task<ActionResult> IndexAsync(int id)
        {

            var rawContent = string.Empty;
            using(var reader = new StreamReader(Request.Body))
            {
                rawContent = reader.ReadToEnd();
            }

            // Set the Blog Root Node ID in the implementation
            _articulateMetaWeblogProvider.ArticulateBlogRootNodeId = id;

            // This service call from the Nuget package - consumes our implmentation/service of Metaweblog above
            string result = await _metaWeblogService.InvokeAsync(rawContent);
            return Content(result, "text/xml", Encoding.UTF8);
        }
    }
}
