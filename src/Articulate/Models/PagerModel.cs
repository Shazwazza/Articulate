#nullable enable
namespace Articulate.Models
{
    public class PagerModel(
        int pageSize,
        int currentIndex,
        int totalPages,
        string? nextUrl = "",
        string? previousUrl = "")
    {
        /// <summary>
        ///     Gets the number of items per page.
        /// </summary>
        public int PageSize { get; } = pageSize;

        /// <summary>
        ///     Gets the total number of pages.
        /// </summary>
        public int TotalPages { get; } = totalPages;

        /// <summary>
        ///     Gets the current page index.
        /// </summary>
        public int CurrentPageIndex { get; } = currentIndex;

        public string? NextUrl { get; } = nextUrl;

        public string? PreviousUrl { get; } = previousUrl;

        public bool HasNext => !string.IsNullOrEmpty(NextUrl);

        public bool HasPrevious => !string.IsNullOrEmpty(PreviousUrl);
    }
}
