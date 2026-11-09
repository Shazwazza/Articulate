#nullable enable
using Articulate.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using Umbraco.Cms.Web.Common.DependencyInjection;

namespace Articulate.Controllers
{
    /// <inheritdoc/>
    public abstract class BlogPostControllerBase : RenderController
    {
        private readonly ILogger<BlogPostControllerBase> _logger;
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IOptions<ArticulateCommentsOptions> _commentsOptions;

        [Obsolete("Use the constructor that accepts IOptions<ArticulateCommentsOptions>. Scheduled for removal in a future release.")]
        protected BlogPostControllerBase(
            ILogger<BlogPostControllerBase> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback)
            : this(logger, compositeViewEngine, umbracoContextAccessor, publishedValueFallback,
                StaticServiceProvider.Instance.GetRequiredService<IOptions<ArticulateCommentsOptions>>())
        {
        }

        protected BlogPostControllerBase(
            ILogger<BlogPostControllerBase> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback,
            IOptions<ArticulateCommentsOptions> commentsOptions)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _logger = logger;
            _publishedValueFallback = publishedValueFallback;
            _commentsOptions = commentsOptions;
        }

        /// <inheritdoc/>
        public override IActionResult Index()
        {
            if (CurrentPage is null)
            {
                _logger.LogWarning("BlogPostControllerBase.Index: CurrentPage is null, returning 404");
                return NotFound();
            }

            var post = new PostModel(CurrentPage, _publishedValueFallback, _commentsOptions.Value);
            return View("Post", post);
        }
    }
}
