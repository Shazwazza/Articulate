#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace Articulate.Controllers
{
    /// <inheritdoc />
    public abstract class BlogPostControllerBase(
        ILogger<BlogPostControllerBase> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedValueFallback publishedValueFallback)
        : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        /// <inheritdoc />
        public override IActionResult Index()
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("BlogPostControllerBase.Index: CurrentPage is null, returning 404");
                return NotFound();
            }

            var post = new PostModel(CurrentPage, publishedValueFallback);
            return View("Post", post);
        }
    }
}
