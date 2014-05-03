using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class PageModel
    {
        public int TotalPages { get; set; }
        public int CurrentPageIndex { get; set; }

        public bool HasNext
        {
            get { return true; }
        }

        public bool HasPrevious
        {
            get { return true; }
        }
    }

    public class BlogModel : ThemedModel
    {
        public BlogModel(IPublishedContent content)
            : base(content)
        {
        }

        public PageModel Pages
        {
            get { return new PageModel(); }
        }

        public string BlogTitle
        {
            get { return Content.GetPropertyValue<string>("blogTitle", true); }
        }

        public string BlogDescription
        {
            get { return Content.GetPropertyValue<string>("blogDescription", true); }
        }

        /// <summary>
        /// Returns the default archive/list URL for blog posts
        /// </summary>
        public string ArchiveUrl
        {
            get { return BlogListNode == null ? null : BlogListNode.Url; }
        }

        /// <summary>
        /// The Home Blog Url
        /// </summary>
        public string RootUrl
        {
            get { return RootBlogNode == null ? null : RootBlogNode.Url; }
        }

        ///// <summary>
        ///// Returns the archive/list URL for blog posts by category
        ///// </summary>
        //public string ArchiveUrlByCategory
        //{
        //    get
        //    {
        //        var current = Content;
        //        while (current.ContentType != null
        //               && current.ContentType.Id > 0
        //               && !current.ContentType.Alias.InvariantEquals("ArticulateList"))
        //        {
        //            current = current.Parent;
        //        }
        //        if (current == null || current.ContentType == null || !current.ContentType.Alias.InvariantEquals("ArticulateList"))
        //        {
        //            throw new InvalidOperationException("Could not find the ArticulateList document for the current rendered Articulate document");
        //        }

        //        return current.Url;
        //    }
        //}

    }
}