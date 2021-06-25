using Articulate.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace Articulate.Controllers
{
    public abstract class BlogPostControllerBase : RenderController
    {
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IVariationContextAccessor _variationContextAccessor;
        private readonly IImageUrlGenerator _imageUrlGenerator;

        protected BlogPostControllerBase(
            ILogger<RenderController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback,
            IVariationContextAccessor variationContextAccessor,
            IImageUrlGenerator imageUrlGenerator)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _publishedValueFallback = publishedValueFallback;
            _variationContextAccessor = variationContextAccessor;
            _imageUrlGenerator = imageUrlGenerator;
        }

        public override ActionResult Index()
        {
            var post = new PostModel(CurrentPage, _publishedValueFallback, _variationContextAccessor, _imageUrlGenerator);
            return View(PathHelper.GetThemeViewPath(post, "Post"), post);
        }
    }
}
