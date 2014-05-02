namespace Articulate.Controllers
{
    public class ArticulateRichTextController : BlogPostControllerBase
    {
        protected override string ViewName
        {
            get { return "RichText"; }
        }
    }
}