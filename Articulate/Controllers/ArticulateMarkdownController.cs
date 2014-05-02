namespace Articulate.Controllers
{
    public class ArticulateMarkdownController : BlogPostControllerBase
    {
        protected override string ViewName
        {
            get { return "Markdown"; }
        }
    }
}