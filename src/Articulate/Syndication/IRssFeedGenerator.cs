#nullable enable
using System.ServiceModel.Syndication;

namespace Articulate.Syndication
{
    /// <summary>
    ///     Defines a service that generates RSS feeds for Articulate.
    /// </summary>
    public interface IRssFeedGenerator
    {
        /// <summary>
        ///     Generates an RSS feed for the specified root page and posts.
        /// </summary>
        /// <param name="rootPageModel">The blog root model.</param>
        /// <param name="posts">Collection of posts to include in the feed.</param>
        /// <returns>A syndication feed representing the RSS output.</returns>
        public SyndicationFeed GetFeed(IMasterModel rootPageModel, IEnumerable<PostModel> posts);
    }
}
