namespace Articulate.Models
{
    public class PagerModel
    {
        public PagerModel(int pageSize, int currentIndex, int totalPages, string nextUrl = null, string previousUrl = null)
        {
            PageSize = pageSize;
            CurrentPageIndex = currentIndex;
            TotalPages = totalPages;
            NextUrl = nextUrl;
            PreviousUrl = previousUrl;
        }

        public int PageSize { get; private set; }

        public int TotalPages { get; private set; }

        public int CurrentPageIndex { get; private set; }

        public string NextUrl { get; private set; }

        public string PreviousUrl { get; private set; }

        public bool HasNext
        {
            get { return NextUrl != null; }
        }

        public bool HasPrevious
        {
            get { return PreviousUrl != null; }
        }
    }
}