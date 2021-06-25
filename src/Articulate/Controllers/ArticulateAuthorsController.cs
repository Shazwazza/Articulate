using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Extensions;

namespace Articulate.Controllers
{
    /// <summary>
    /// This is used to redirect the Authors node to the root so no 404s occur
    /// </summary>
    public class ArticulateAuthorsController : RenderController
    {
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IVariationContextAccessor _variationContextAccessor;

        public ArticulateAuthorsController(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _publishedValueFallback = publishedValueFallback;
            _variationContextAccessor = variationContextAccessor;
        }

        public override IActionResult Index()
        {
            var root = new MasterModel(
                CurrentPage,
                _publishedValueFallback,
                _variationContextAccessor);

            //TODO: Should we have another setting for authors?
            if (root.RootBlogNode.Value<bool>("redirectArchive"))
            {
                return RedirectPermanent(root.RootBlogNode.Url());
            }

            //default

            var action = ControllerContext.RouteData.Values["action"].ToString();
            if (!EnsurePhsyicalViewExists(action))
                return new UmbracoNotFoundResult();

            return View(action, new ContentModel(CurrentPage));
        }
    }
}
