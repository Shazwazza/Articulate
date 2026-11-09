#nullable enable
using Articulate.Options;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Articulate.Controllers
{
    /// <summary>
    /// Controller for Articulate Markdown blog posts.
    /// </summary>
    public class ArticulateMarkdownController : BlogPostControllerBase
    {
        [Obsolete("Use the constructor that accepts IOptions<ArticulateCommentsOptions>. Scheduled for removal in a future release.")]
        public ArticulateMarkdownController(
            ILogger<ArticulateMarkdownController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedValueFallback)
        {
        }

        [ActivatorUtilitiesConstructor]
        public ArticulateMarkdownController(
            ILogger<ArticulateMarkdownController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback,
            IOptions<ArticulateCommentsOptions> commentsOptions)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedValueFallback, commentsOptions)
        {
        }
    }
}
