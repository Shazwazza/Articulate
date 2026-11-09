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
    /// Controller for Articulate Rich Text blog posts.
    /// </summary>
    public class ArticulateRichTextController : BlogPostControllerBase
    {
        [Obsolete("Use the constructor that accepts IOptions<ArticulateCommentsOptions>. Scheduled for removal in a future release.")]
        public ArticulateRichTextController(
            ILogger<ArticulateRichTextController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedValueFallback)
        {
        }

        [ActivatorUtilitiesConstructor]
        public ArticulateRichTextController(
            ILogger<ArticulateRichTextController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedValueFallback publishedValueFallback,
            IOptions<ArticulateCommentsOptions> commentsOptions)
            : base(logger, compositeViewEngine, umbracoContextAccessor, publishedValueFallback, commentsOptions)
        {
        }
    }
}
