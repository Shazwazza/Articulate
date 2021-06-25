using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Macros;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.Macros;

namespace Articulate.Controllers
{
    /// <summary>
    /// Summary description for FeedController
    /// </summary>
    public class FeedController : PluginController
    {
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IVariationContextAccessor _variationContextAccessor;
        private readonly IImageUrlGenerator _imageUrlGenerator;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly PartialViewMacroEngine _partialViewMacroEngine;

        public FeedController(
            IUmbracoContextAccessor umbracoContextAccessor,
            IUmbracoDatabaseFactory databaseFactory,
            ServiceContext services,
            AppCaches appCaches,
            IProfilingLogger profilingLogger,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            IImageUrlGenerator imageUrlGenerator,
            UmbracoHelper umbracoHelper,
            PartialViewMacroEngine partialViewMacroEngine)
            : base(umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger)
        {
            _publishedValueFallback = publishedValueFallback;
            _variationContextAccessor = variationContextAccessor;
            _imageUrlGenerator = imageUrlGenerator;
            _umbracoHelper = umbracoHelper;
            _partialViewMacroEngine = partialViewMacroEngine;
        }

        [HttpGet]
        // TODO: This won't work anymore
        //[OutputCache(Duration = 120)]
        public IActionResult RenderGitHub(int id)
        {
            var content = _umbracoHelper.Content(id);
            if (content == null)
            {
                return NotFound();
            }

            var articulateModel = new MasterModel(content, _publishedValueFallback, _variationContextAccessor);
            var viewPath = PathHelper.GetThemePartialViewPath(articulateModel, "FeedGitHub");
            
            return Content(RenderViewToString(content, this, viewPath));
        }        

        /// <summary>
		/// Renders the partial view to string.
		/// </summary>
		/// <param name="controller">The controller context.</param>
		/// <param name="viewName">Name of the view.</param>
		/// <param name="model">The model.</param>
		/// <param name="isPartial">true if it is a Partial view, otherwise false for a normal view </param>
		/// <returns></returns>
        private string RenderViewToString(IPublishedContent content, ControllerBase controller, string viewName)
        {
            MacroContent result = _partialViewMacroEngine.Execute(new MacroModel
            {
                Alias = nameof(FeedController),
                Name = nameof(FeedController),
                Id = 1,
                MacroSource = viewName
            }, content);

            return result.Text;
        }
    }
}
