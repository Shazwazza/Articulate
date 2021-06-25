using System;
using System.Collections.Generic;
using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.Routing;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    public class MarkdownEditorController : PluginController
    {
        private readonly UmbracoApiControllerTypeCollection _apiControllers;
        private readonly LinkGenerator _linkGenerator;

        public MarkdownEditorController(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            LinkGenerator linkGenerator)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger)
            => _linkGenerator = linkGenerator;

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

        /// <summary>
        /// Gets the current page.
        /// </summary>
        private IPublishedContent CurrentPage
        {
            get
            {
                UmbracoRouteValues umbracoRouteValues = HttpContext.Features.Get<UmbracoRouteValues>();
                if (umbracoRouteValues is null)
                {
                    throw new InvalidOperationException($"No {nameof(UmbracoRouteValues)} feature was found in the HttpContext");
                }

                return umbracoRouteValues.PublishedRequest.PublishedContent;
            }
        }
    }
}
