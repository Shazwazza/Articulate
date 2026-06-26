#nullable enable
using Articulate.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.Controllers;

namespace Articulate.Controllers
{
    /// <summary>
    /// Handles the ArticulateAuthors container node (/authors/).
    /// </summary>
    /// <remarks>
    /// If a custom theme provides Authors.cshtml, it will render the author listing.
    /// Otherwise, redirects to the blog root.
    /// Individual author pages (/authors/john-doe/) are handled by <see cref="ArticulateAuthorController"/>
    /// </remarks>
    public class ArticulateAuthorsController(
        ILogger<ArticulateAuthorsController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        UmbracoHelper umbracoHelper,
        AppCaches appCaches,
        IPublishedValueFallback publishedValueFallback,
        IOptions<ArticulateCommentsOptions> commentsOptions)
        : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        private AppCaches AppCaches { get; } = appCaches;

        /// <inheritdoc/>
        public override IActionResult Index()
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("ArticulateAuthorsController.Index: CurrentPage is null, returning 404");
                return NotFound();
            }

            var root = new MasterModel(CurrentPage, publishedValueFallback, commentsOptions.Value);

            // Check if theme has custom Authors.cshtml view before building the listing model.
            if (!EnsurePhysicalViewExists("Authors"))
            {
                logger.LogInformation(
                    "ArticulateAuthorsController: No Authors.cshtml view found for Articulate root {RootId} using theme '{Theme}'. " +
                    "Redirecting authors directory to blog root.",
                    root.RootBlogNode.Id,
                    root.Theme);

                return RedirectPermanent(root.RootBlogNode.Url());
            }

            IPublishedContent[] listNodes = root.RootBlogNode.Children()
                .Where(x => x.ContentType.Alias == ArticulateConstants.ContentType.ArticulateArchive)
                .ToArray();
            int[] listNodeIds = listNodes.Select(x => x.Id).ToArray();
            IReadOnlyDictionary<string, (int PostCount, DateTime? LastPostDate)> authorStats =
                GetAuthorPostStats(root.RootBlogNode.Id, listNodeIds);

            var authorNodes = CurrentPage.Children().ToList();
            var authors = authorNodes
                .Select(a =>
                {
                    var authorKey = UmbracoHelperExtensions.NormalizeAuthorName(a.Name);
                    _ = authorStats.TryGetValue(authorKey, out (int PostCount, DateTime? LastPostDate) stats);

                    Umbraco.Cms.Core.Models.MediaWithCrops? image = a.Value<Umbraco.Cms.Core.Models.MediaWithCrops>("authorImage");

                    return new AuthorDirectoryItemModel
                    {
                        Name = a.Name,
                        Bio = a.Value<string>("authorBio") ?? string.Empty,
                        AuthorUrl = a.Value<string>("authorUrl").ToSafeHrefUrl(),
                        BlogUrl = a.Url(),
                        AuthorRssUrl = root.RootBlogNode.Url(mode: UrlMode.Absolute).EnsureEndsWith('/') + "author/" + a.Id + "/rss",
                        Image = image,
                        CroppedWideUrl = image?.GetCropUrl(cropAlias: "wide", preferFocalPoint: true, useCropDimensions: true) ?? string.Empty,
                        PostCount = stats.PostCount,
                        LastPostDate = stats.LastPostDate
                    };
                })
                .ToList();

            var model = new AuthorDirectoryModel(CurrentPage, publishedValueFallback, commentsOptions.Value)
            {
                Authors = authors
            };

            return View("Authors", model);
        }

        private IReadOnlyDictionary<string, (int PostCount, DateTime? LastPostDate)> GetAuthorPostStats(
            int rootBlogNodeId,
            int[] listNodeIds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rootBlogNodeId);

            IReadOnlyDictionary<string, (int PostCount, DateTime? LastPostDate)> GetResult() =>
                umbracoHelper.GetAuthorPostStatsByAuthor(listNodeIds);

#if DEBUG
            return GetResult();
#else
            return (IReadOnlyDictionary<string, (int PostCount, DateTime? LastPostDate)>)AppCaches.RuntimeCache.Get(
                string.Concat(
                    nameof(ArticulateAuthorsController),
                    nameof(UmbracoHelperExtensions.GetAuthorPostStatsByAuthor),
                    rootBlogNodeId,
                    string.Join(",", listNodeIds.Order())),
                GetResult,
                TimeSpan.FromSeconds(30))!;
#endif
        }
    }
}
