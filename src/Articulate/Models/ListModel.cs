#nullable enable
using Articulate.Options;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// Represents a page that displays a list of blog posts
    /// </summary>
    public class ListModel : MasterModel
    {
        private readonly IEnumerable<IPublishedContent> _listItems;
        private readonly Lazy<PostModel[]> _posts;

        /// <summary>
        /// Accepts an explicit list of child items
        /// </summary>
        /// <param name="content"></param>
        /// <param name="listItems"></param>
        /// <param name="pager"></param>
        /// <param name="publishedValueFallback"></param>
        /// <param name="commentsOptions"></param>
        /// <remarks>
        /// Default sorting by published date will be disabled for this list model, it is assumed that the list items will
        /// already be sorted.
        /// </remarks>
        public ListModel(
            IPublishedContent content,
            PagerModel? pager,
            IEnumerable<IPublishedContent>? listItems,
            IPublishedValueFallback publishedValueFallback,
            ArticulateCommentsOptions? commentsOptions = null)
            : base(content, publishedValueFallback, commentsOptions)
        {
            ArgumentNullException.ThrowIfNull(content);

            Pages = pager ?? throw new ArgumentNullException(nameof(pager));
            _listItems = listItems ?? throw new ArgumentNullException(nameof(listItems));

            var contentName = content.Name;
            if (content.ContentType.Alias.Equals(ArticulateConstants.ContentType.ArticulateArchive))
            {
                PageTitle = BlogTitle + " - " + BlogDescription;
            }
            else
            {
                PageTags = contentName;
            }

            _posts = new Lazy<PostModel[]>(BuildPosts, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Gets the pager model
        /// </summary>
        public PagerModel? Pages { get; }

        /// <summary>
        /// Gets a strongly typed access to the list of blog posts
        /// </summary>
        public IEnumerable<PostModel> Posts => _posts.Value;

        private PostModel[] BuildPosts()
        {
            IEnumerable<IPublishedContent> items = _listItems;
            if (Pages is not null)
            {
                // Apply page size limit to the pre-filtered list items
                items = items.Take(Pages.PageSize);
            }

            return items
                .Select(x => new PostModel(x, PublishedValueFallback, CommentsOptions))
                .ToArray();
        }
    }
}
