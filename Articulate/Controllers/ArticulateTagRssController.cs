using System;
using System.Web.Mvc;
using Argotic.Syndication;
using Articulate.Models;
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
    public class ArticulateTagRssController : RenderMvcController
    {
        public ActionResult Categories(RenderModel model, string tag)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (tag == null) throw new ArgumentNullException("tag");

            return RenderTagsOrCategoriesRss(model, "ArticulateCategories", "categories");
        }

        public ActionResult Tags(RenderModel model, string tag)
        {
            if (model == null) throw new ArgumentNullException("model");
            if (tag == null) throw new ArgumentNullException("tag");

            return RenderTagsOrCategoriesRss(model, "ArticulateTags", "tags");
        }

        public ActionResult RenderTagsOrCategoriesRss(RenderModel model, string tagGroup, string baseUrl)
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
                    Generator = "Articulate blog engine, powered by Umbraco"
                }               
            };

            foreach (var post in contentByTag.Posts)
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