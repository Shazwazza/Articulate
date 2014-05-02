using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public abstract class BlogPostControllerBase : RenderMvcController
    {

        public override ActionResult Index(RenderModel model)
        {
            var post = new Post(model);
            return View(PathHelper.GetThemeViewPath(post, ViewName), post);
        }

        protected abstract string ViewName { get; }
    }

    public class ArticulateMarkdownController : BlogPostControllerBase
    {
        protected override string ViewName
        {
            get { return "Markdown"; }
        }
    }

    public class ArticulateRichTextController : BlogPostControllerBase
    {
        protected override string ViewName
        {
            get { return "RichText"; }
        }
    }
}
