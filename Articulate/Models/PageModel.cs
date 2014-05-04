namespace Articulate.Models
{
    public class PageModel
    {
        public int TotalPages { get; set; }
        public int CurrentPageIndex { get; set; }

        public bool HasNext
        {
            get { return true; }
        }

        public bool HasPrevious
        {
            get { return true; }
        }
    }
}