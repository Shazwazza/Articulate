#nullable enable
using System.Globalization;
using System.ServiceModel.Syndication;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Syndication
{
    /// <summary>
    /// Default RSS feed generator for Articulate.
    /// </summary>
    public class RssFeedGenerator(ILogger<RssFeedGenerator> logger, IHostingEnvironment hostingEnvironment)
        : IRssFeedGenerator
    {
        /// <inheritdoc/>
        public SyndicationFeed GetFeed(IMasterModel rootPageModel, IEnumerable<PostModel> posts)
        {
            var feed = new SyndicationFeed(
                rootPageModel.BlogTitle,
                rootPageModel.BlogDescription,
                new Uri(rootPageModel.RootBlogNode.Url(mode: UrlMode.Absolute)),
                GetFeedItems(rootPageModel, posts))
            {
                Generator = "Articulate, blogging built on Umbraco", ImageUrl = GetBlogImage(rootPageModel),
            };
            return feed;
        }

        /// <summary>
        /// Gets the HTML content for a post to be included in the feed.
        /// </summary>
        /// <param name="model">The post model.</param>
        /// <returns>A string containing the HTML content.</returns>
        protected virtual string GetPostContent(PostModel model) => model.Body.ToHtmlString();

        /// <summary>
        /// Converts a <see cref="PostModel"/> into a <see cref="SyndicationItem"/>.
        /// </summary>
        /// <param name="post">The post to convert.</param>
        /// <param name="rootUrl">The absolute root URL of the blog.</param>
        /// <returns>A syndication item, or null if the post URL cannot be resolved.</returns>
        protected virtual SyndicationItem? GetFeedItem(PostModel post, string rootUrl)
        {
            var posturl = post.Url(mode: UrlMode.Absolute);

            // Cannot continue if the url cannot be resolved - probably has publishing issues
            if (posturl is ['#', ..])
            {
                return null;
            }

            var appPath = hostingEnvironment.ApplicationVirtualPath;
            var rootUri = new Uri(rootUrl);
            var mediaRoot = rootUri.GetLeftPart(UriPartial.Authority) + appPath.EnsureStartsWith('/').TrimEnd('/');

            var contentHtml = GetPostContent(post);
            var rootUrlTrimmed = rootUrl.TrimEnd('/');

            var content = RssFeedGeneratorRegexes.RelativeMediaHrefRegex().Replace(
                contentHtml,
                match => match.Groups.Count == 2
                    ? $" href=\"{rootUrlTrimmed}{match.Groups[1].Value}\""
                    : match.Value);
            content = RssFeedGeneratorRegexes.RelativeMediaSrcRegex().Replace(
                content,
                match => match.Groups.Count == 2
                    ? $" src=\"{mediaRoot}{match.Groups[1].Value}\""
                    : match.Value);

            var item = new SyndicationItem(
                post.Name,
                new TextSyndicationContent(content, TextSyndicationContentKind.Html),
                new Uri(posturl),
                post.Id.ToString(CultureInfo.InvariantCulture),
                post.PublishedDate)
            {
                PublishDate = post.PublishedDate,

                // don't include this as it will override the main content bits
                // Summary = new TextSyndicationContent(post.Excerpt)
            };
            foreach (var c in post.Categories)
            {
                item.Categories.Add(new SyndicationCategory(c));
            }

            return item;
        }

        /// <summary>
        /// Resolves the absolute URI for the blog logo.
        /// </summary>
        /// <param name="rootPageModel">The blog root model.</param>
        /// <returns>The logo URI, or null if not set or invalid.</returns>
        protected virtual Uri? GetBlogImage(IMasterModel rootPageModel)
        {
            Uri? logoUri = null;
            try
            {
                logoUri = rootPageModel.BlogLogo.IsNullOrWhiteSpace()
                    ? null
                    : new Uri(rootPageModel.BlogLogo, UriKind.RelativeOrAbsolute);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not convert the blog logo path to a Uri");
            }

            return logoUri;
        }

        private List<SyndicationItem> GetFeedItems(IMasterModel model, IEnumerable<PostModel> posts)
        {
            var rootUrl = model.RootBlogNode.Url(mode: UrlMode.Absolute);
            IEnumerable<PostModel> postModels = posts as PostModel[] ?? [.. posts];
            return !postModels.Any() ? [] : [.. postModels.Select(post => GetFeedItem(post, rootUrl)).WhereNotNull()];
        }
    }
}
