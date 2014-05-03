using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class TagPage : PublishedContentBase
    {
        private readonly IPublishedContent _parent;

        public TagPage(IPublishedContent parent)
        {
            _parent = parent;
        }

        public override IPublishedProperty GetProperty(string alias)
        {
            return null;
        }

        public override PublishedItemType ItemType
        {
            get { return PublishedItemType.Content; }
        }

        public override bool IsDraft
        {
            get { return false; }
        }

        public override IPublishedContent Parent
        {
            get { throw new NotImplementedException(); }
        }

        public override IEnumerable<IPublishedContent> Children
        {
            get { throw new NotImplementedException(); }
        }

        public override ICollection<IPublishedProperty> Properties
        {
            get { throw new NotImplementedException(); }
        }

        public override PublishedContentType ContentType
        {
            get { throw new NotImplementedException(); }
        }

        public override int Id
        {
            get { throw new NotImplementedException(); }
        }

        public override int TemplateId
        {
            get { throw new NotImplementedException(); }
        }

        public override int SortOrder
        {
            get { throw new NotImplementedException(); }
        }

        public override string Name
        {
            get { throw new NotImplementedException(); }
        }

        public override string UrlName
        {
            get { throw new NotImplementedException(); }
        }

        public override string DocumentTypeAlias
        {
            get { return "ArticulateTags"; }
        }

        public override int DocumentTypeId
        {
            get { throw new NotImplementedException(); }
        }

        public override string WriterName
        {
            get { throw new NotImplementedException(); }
        }

        public override string CreatorName
        {
            get { throw new NotImplementedException(); }
        }

        public override int WriterId
        {
            get { throw new NotImplementedException(); }
        }

        public override int CreatorId
        {
            get { throw new NotImplementedException(); }
        }

        public override string Path
        {
            get { throw new NotImplementedException(); }
        }

        public override DateTime CreateDate
        {
            get { throw new NotImplementedException(); }
        }

        public override DateTime UpdateDate
        {
            get { throw new NotImplementedException(); }
        }

        public override Guid Version
        {
            get { throw new NotImplementedException(); }
        }

        public override int Level
        {
            get { throw new NotImplementedException(); }
        }
    }

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
        public BlogModel(IPublishedContent content) : base(content)
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