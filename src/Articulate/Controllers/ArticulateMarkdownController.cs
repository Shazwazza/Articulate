#nullable enable
using Articulate.Options;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace Articulate.Controllers
{
    /// <summary>
    /// Controller for Articulate Markdown blog posts.
    /// </summary>
    public class ArticulateMarkdownController(
        ILogger<ArticulateMarkdownController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedValueFallback publishedValueFallback,
        IOptions<ArticulateCommentsOptions> commentsOptions)
        : BlogPostControllerBase(logger, compositeViewEngine, umbracoContextAccessor, publishedValueFallback, commentsOptions);
}
