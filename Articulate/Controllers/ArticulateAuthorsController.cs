using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    [OutputCache(Duration = 60, VaryByHeader = "host")]
    public class ArticulateAuthorsController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {   
            var authorList = new AuthorListModel(model.Content);
            authorList.Authors = Umbraco.GetContentByAuthors(authorList);
            return View(PathHelper.GetThemeViewPath(authorList, "Authors"), authorList);
        }
    }
}
