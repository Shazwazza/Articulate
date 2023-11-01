using System.Collections.Generic;
using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    public class MarkdownEditorController : RenderController
    {
        private readonly UmbracoApiControllerTypeCollection _apiControllers;
        private readonly LinkGenerator _linkGenerator;

        public MarkdownEditorController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            LinkGenerator linkGenerator)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _linkGenerator = linkGenerator;
        }

        [HttpGet]
        public IActionResult NewPost()
        {
            var vm = new MarkdownEditorInitModel
            {
                ArticulateNodeId = CurrentPage.Id,
                PostUrl = _linkGenerator.GetUmbracoApiService<MardownEditorApiController>(controller => controller.PostNew()),
                IsAuthUrl = _linkGenerator.GetUmbracoControllerUrl(nameof(AuthenticationController.IsAuthenticated), typeof(AuthenticationController)),
                DoAuthUrl = _linkGenerator.GetUmbracoControllerUrl(
                    nameof(AuthenticationController.PostLogin),
                    typeof(AuthenticationController),
                    new Dictionary<string, object> { ["loginModel"] = null })
            };
            
            return View("~/App_Plugins/Articulate/Views/MarkdownEditor.cshtml", vm);
        }
    }
}
