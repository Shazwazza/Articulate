using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public abstract class BlogPostControllerBase : RenderMvcController
    {

        public override ActionResult Index(RenderModel model)
        {   
            var post = new PostModel(model.Content);
            
            //store the theme for retrieval in the theme engine
            HttpContext.Items["theme"] = post.Theme;
            
            return View("Post", post);
        }

    }
}
