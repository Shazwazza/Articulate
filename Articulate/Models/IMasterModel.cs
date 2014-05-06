using Umbraco.Core.Models;

namespace Articulate.Models
{
    public interface IMasterModel
    {
        /// <summary>
        /// Returns the current theme
        /// </summary>
        string Theme { get; }

        IPublishedContent RootBlogNode { get; }
        IPublishedContent BlogListNode { get; }

        string Name { get; }
        string BlogTitle { get; }
        string BlogDescription { get; }
    }
}