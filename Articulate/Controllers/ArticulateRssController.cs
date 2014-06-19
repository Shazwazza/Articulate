using System;
using System.Linq;
using System.Web.Mvc;
using Argotic.Syndication;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Rss controller
    /// </summary>
    /// <remarks>
    /// Cached for one minute
    /// </remarks>
    [OutputCache(Duration = 300)]
    public class ArticulateRssController : RenderMvcController
    {

        //NonAction so it is not routed since we want to use an overload below
        [NonAction]
        public override ActionResult Index(RenderModel model)
        {
            return base.Index(model);
        }

        public ActionResult Index(RenderModel model, int? maxItems)
        {
            if (!maxItems.HasValue) maxItems = 25;

            var listNode = model.Content.Children
               .FirstOrDefault(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive"));
            if (listNode == null)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            var rootPageModel = new ListModel(listNode, new PagerModel(maxItems.Value, 0, 1));

            var feed = new RssFeed
            {
                Channel =
                {
                    Link = new Uri(rootPageModel.RootBlogNode.UrlWithDomain()),
                    Title = rootPageModel.BlogTitle,
                    Description = rootPageModel.BlogDescription,
                    Generator = "Articulate, blogging built on Umbraco"
                }
            };

            foreach (var post in rootPageModel.Children<PostModel>())
            {
                var item = new RssItem
                {
                    Title = post.Name,
                    Link = new Uri(post.UrlWithDomain()),
                    Description = post.Excerpt
                };
                feed.Channel.AddItem(item);
            }

            return new RssResult(feed);
        }

        public ActionResult Categories(RenderModel model, string tag, int? maxItems)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (tag == null) throw new ArgumentNullException("tag");

            if (!maxItems.HasValue) maxItems = 25;

            return RenderTagsOrCategoriesRss(model, "ArticulateCategories", "categories", maxItems.Value);
        }

        public ActionResult Tags(RenderModel model, string tag, int? maxItems)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (tag == null) throw new ArgumentNullException("tag");

            if (!maxItems.HasValue) maxItems = 25;

            return RenderTagsOrCategoriesRss(model, "ArticulateTags", "tags", maxItems.Value);
        }

        public ActionResult RenderTagsOrCategoriesRss(RenderModel model, string tagGroup, string baseUrl, int maxItems)
        {
            var tagPage = model.Content as ArticulateVirtualPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new ListModel(model.Content.Parent);

            var contentByTag = Umbraco.GetContentByTag(
                rootPageModel,
                tagPage.Name,
                tagGroup,
                baseUrl);

            var feed = new RssFeed
            {
                Channel =
                {
                    Link = new Uri(rootPageModel.RootBlogNode.UrlWithDomain()),
                    Title = rootPageModel.BlogTitle,
                    Description = rootPageModel.BlogDescription,
                    Generator = "Articulate, blogging built on Umbraco"
                }               
            };

            foreach (var post in contentByTag.Posts.Take(maxItems))
            {
                var item = new RssItem
                {
                    Title = post.Name,
                    Link = new Uri(post.UrlWithDomain()),
                    Description = post.Excerpt
                };
                feed.Channel.AddItem(item);
            }

            return new RssResult(feed);
        }

        internal class RssResult : ActionResult
        {
            private readonly RssFeed _feed;

            public RssResult(RssFeed feed)
            {
                _feed = feed;
            }

            public override void ExecuteResult(ControllerContext context)
            {
                context.HttpContext.Response.ContentType = "application/rss+xml";
                _feed.Save(context.HttpContext.Response.OutputStream);
            }
        }
    }
}