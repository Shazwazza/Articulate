using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core.Persistence;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    public class ArticulateTagsController : RenderMvcController
    {
        public override ActionResult Index(RenderModel model)
        {
            var tagPage = model.Content as TagPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(TagPage));
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTags = Umbraco.GetContentByTags(rootPageModel, "ArticulateTags");

            var tagListModel = new TagListModel(
                rootPageModel,
                tagPage.Name,
                contentByTags);
            
            return View(PathHelper.GetThemeViewPath(tagListModel, "Tags"), tagListModel);
        }

        public ActionResult Tag(RenderModel model)
        {
            var tagPage = model.Content as TagPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(TagPage));
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTag = Umbraco.GetContentByTag(
                rootPageModel, 
                tagPage.Name,
                "ArticulateTags");

            if (contentByTag == null)
            {
                return new HttpNotFoundResult();
            }
            var pageSize = 1;
            var pager = new PagerModel(pageSize, 0, contentByTag.PostCount);

            var listModel = new ListModel(tagPage, contentByTag.Posts, pager);

            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }
    }
}