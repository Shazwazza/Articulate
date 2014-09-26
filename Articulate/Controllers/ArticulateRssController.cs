using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Linq;
using Articulate.Models;
using Umbraco.Core;
using Umbraco.Core.Logging;
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
#if (!DEBUG)
    [OutputCache(Duration = 300)]
#endif
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

            var feed = GetFeed(rootPageModel, rootPageModel.Children<PostModel>());

            return new RssResult(feed, rootPageModel);
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

            var feed = GetFeed(rootPageModel, contentByTag.Posts.Take(maxItems));

            return new RssResult(feed, rootPageModel);
        }

        /// <summary>
        /// Returns the XSLT to render the RSS nicely in a browser
        /// </summary>
        /// <returns></returns>
        public ActionResult FeedXslt()
        {
            var result = Resources.FeedXslt;
            return Content(result, "text/xml");
        }

        private SyndicationFeed GetFeed(IMasterModel rootPageModel, IEnumerable<PostModel> posts)
        {
            var feed = new SyndicationFeed(
               rootPageModel.BlogTitle,
               rootPageModel.BlogDescription,
               new Uri(rootPageModel.RootBlogNode.UrlWithDomain()),
               GetFeedItems(rootPageModel, posts))
            {
                Generator = "Articulate, blogging built on Umbraco",
                ImageUrl = GetBlogImage(rootPageModel)                
            };

            //TODO: attempting to add media:thumbnail...
            //feed.AttributeExtensions.Add(new XmlQualifiedName("media", "http://www.w3.org/2000/xmlns/"), "http://search.yahoo.com/mrss/");

            return feed;
        }

        private Regex _relativeMediaSrc = new Regex(" src=(?:\"|')(/media/.*?)(?:\"|')", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex _relativeMediaHref = new Regex(" href=(?:\"|')(/media/.*?)(?:\"|')", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private IEnumerable<SyndicationItem> GetFeedItems(IMasterModel model, IEnumerable<PostModel> posts)
        {
            var rootUrl = model.RootBlogNode.UrlWithDomain();

            var result = new List<SyndicationItem>();
            foreach (var post in posts)
            {
                var content = _relativeMediaHref.Replace(post.Body.ToHtmlString(), match =>
                {
                    if (match.Groups.Count == 2)
                    {
                        return " href=\"" +
                               rootUrl.TrimEnd('/') + match.Groups[1].Value.EnsureStartsWith('/') +
                               "\"";
                    }
                    return null;
                });
                content = _relativeMediaSrc.Replace(content, match =>
                {
                    if (match.Groups.Count == 2)
                    {
                        return " src=\"" +
                               rootUrl.TrimEnd('/') + match.Groups[1].Value.EnsureStartsWith('/') +
                               "\"";
                    }
                    return null;
                });

                var item = new SyndicationItem(
                    post.Name,
                    new TextSyndicationContent(content, TextSyndicationContentKind.Html),
                    new Uri(post.UrlWithDomain()),
                    post.Id.ToString(CultureInfo.InvariantCulture),
                    post.PublishedDate)
                {
                    PublishDate = post.PublishedDate,
                    
                    //don't include this as it will override the main content bits
                    //Summary = new TextSyndicationContent(post.Excerpt)
                };

                //TODO: attempting to add media:thumbnail...
                //item.ElementExtensions.Add(new SyndicationElementExtension("thumbnail", "http://search.yahoo.com/mrss/", "This is a test!"));

                foreach (var c in post.Categories)
                {
                    item.Categories.Add(new SyndicationCategory(c));    
                }
                

                result.Add(item);
            }

            return result;
        } 

        private Uri GetBlogImage(IMasterModel rootPageModel)
        {
            Uri logoUri = null;
            try
            {
                logoUri = rootPageModel.BlogLogo.IsNullOrWhiteSpace()
                    ? null
                    : new Uri(rootPageModel.BlogLogo);
            }
            catch (Exception ex)
            {
                LogHelper.Error<ArticulateRssController>("Could not convert the blog logo path to a Uri", ex);
            }
            return logoUri;
        }

        internal class RssResult : ActionResult
        {
            private readonly SyndicationFeed _feed;
            private readonly IMasterModel _model;

            public RssResult(SyndicationFeed feed, IMasterModel model)
            {
                _feed = feed;
                _model = model;
            }

            public override void ExecuteResult(ControllerContext context)
            {
                context.HttpContext.Response.ContentType = "application/xml";

                using (var txtWriter = new Utf8StringWriter())
                {
                    var xmlWriter = XmlWriter.Create(txtWriter, new XmlWriterSettings
                    {
                        Encoding = Encoding.UTF8,
                        Indent = true,
                        OmitXmlDeclaration = false
                    });

                    // Write the Processing Instruction node.
                    var xsltHeader = string.Format("type=\"text/xsl\" href=\"{0}\"", _model.RootBlogNode.UrlWithDomain().EnsureEndsWith('/') + "rss/xslt");
                    xmlWriter.WriteProcessingInstruction("xml-stylesheet", xsltHeader);

                    var formatter = _feed.GetRss20Formatter();
                    formatter.WriteTo(xmlWriter);
                    
                    xmlWriter.Flush();

                    context.HttpContext.Response.Write(txtWriter.ToString());
                }                
            }

            public sealed class Utf8StringWriter : StringWriter
            {
                public override Encoding Encoding { get { return Encoding.UTF8; } }
            }
        }
    }
}