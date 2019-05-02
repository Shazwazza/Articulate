using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate.Models
{
    public interface IMasterModel : IPublishedContent
    {
        /// <summary>
        /// Returns the current theme
        /// </summary>
        string Theme { get; }
        IPublishedContent RootBlogNode { get; }
        IPublishedContent BlogArchiveNode { get; }
        IPublishedContent BlogAuthorsNode { get; }
        string BlogTitle { get; }
        string BlogDescription { get; }
        string BlogLogo { get; }
        string BlogBanner { get; }
        int PageSize { get; }
        string DisqusShortName { get; }
        string CustomRssFeed { get; }

        string PageTitle { get; }
        string PageDescription { get; }
        string PageTags { get; }
    }
}