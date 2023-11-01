using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Articulate.MetaWeblog;
using Examine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using WilderMinds.MetaWeblog;

namespace Articulate.Controllers
{
    /// <summary>
    /// Custom controller to handle the webblog endpoints so that we can wire
    /// up the articulate start node for the IMetaWeblogProvider data source.
    /// </summary>
    /// <remarks>
    /// The nuget package we use https://github.com/shawnwildermuth/MetaWeblog has
    /// middleware but that just supports one endpoint, we are basically wrapping that
    /// with our own multi-tenanted version.
    /// </remarks>
    public class MetaWeblogController : RenderController
    {
        private readonly IServiceProvider _serviceProvider;

        public MetaWeblogController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IServiceProvider serviceProvider)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _serviceProvider = serviceProvider;
        }

        [HttpPost]
        public async Task<ActionResult> IndexAsync(int id)
        {
            if (id <= 0)
            {
                return Problem("Invalid root node id");
            }

            // create the provider using the start node
            var provider = ActivatorUtilities.CreateInstance<ArticulateMetaWeblogProvider>(
                _serviceProvider,
                id);

            // create the service using the provider
            var service = ActivatorUtilities.CreateInstance<MetaWeblogService>(_serviceProvider, provider);

            var rawContent = string.Empty;
            using(var reader = new StreamReader(Request.Body))
            {
                rawContent = reader.ReadToEnd();
            }

            string result = await service.InvokeAsync(rawContent);
            return Content(result, "text/xml", Encoding.UTF8);
        }
    }
}
