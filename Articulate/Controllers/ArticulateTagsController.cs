using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using Articulate.Models;
using Umbraco.Core.Persistence;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;
using TagModel = Articulate.Models.TagModel;

namespace Articulate.Controllers
{
    public class ArticulateTagsController : RenderMvcController
    {
        

        public override ActionResult Index(RenderModel model)
        {
            var contentByTags = Umbraco.GetContentByTags(new BlogModel(model.Content.Parent), "ArticulateTags");

            //create a blog model of the main page
            var blogModel = new BlogModel(model.Content.Parent);
            var tagListModel = new TagListModel(
                blogModel,
                "Tags",
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
            var contentForTag = Umbraco.GetContentForTag(
                new BlogModel(model.Content.Parent), 
                tagPage.Name,
                "ArticulateTags");

            if (contentForTag == null)
            {
                return new HttpNotFoundResult();
            }

            var listModel = new ListModel(tagPage, contentForTag.Posts);

            return View(PathHelper.GetThemeViewPath(listModel, "List"), listModel);
        }
    }
}