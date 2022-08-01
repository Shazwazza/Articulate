using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private readonly MetaWeblogService _metaWeblogService;

        public MetaWeblogController(ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor, MetaWeblogService metaWeblogService, IExamineManager examineManager)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
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

            // TODO: Pass the ID to the metaWeblogService

            string result = await _metaWeblogService.InvokeAsync(rawContent);
            return Content(result, "text/xml", Encoding.UTF8);
        }
    }
}
