using System;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// Used to create a fake dynamic umbraco page for rendering tag lists, tag pages and search results (any virtual route)
    /// </summary>
    internal class ArticulateVirtualPage : PublishedContentWrapped
    {
        private readonly string _pageName;
        private readonly string _urlPath;

        public ArticulateVirtualPage(IPublishedContent rootBlogPage, string pageName, string pageTypeAlias, string urlPath = null)
            : base(rootBlogPage)
        {
            if (pageName == null) throw new ArgumentNullException("pageName");
            if (pageTypeAlias == null) throw new ArgumentNullException("pageTypeAlias");
            _pageName = pageName;
            DocumentTypeAlias = pageTypeAlias;

            if (urlPath != null)
            {
                _urlPath = urlPath.SafeEncodeUrlSegments();
            }
            
        }
        
        public override string Url => base.Url.EnsureEndsWith('/') + (_urlPath ?? UrlName);

        /// <summary>
        /// Returns the content that was used to create this virtual node - we'll assume this virtual node's parent is based on the real node that created it
        /// </summary>
        public override IPublishedContent Parent => Content;

        public override int Id => int.MaxValue - Parent.Id;

        public override string Name => _pageName;

        public override string UrlName => _pageName.ToLowerInvariant();

        public override string DocumentTypeAlias { get; }

        public override int DocumentTypeId => int.MaxValue - Parent.DocumentTypeId;

        public override string Path => Content.Path.EnsureEndsWith(',') + Id;

        public override int Level => Content.Level + 1;
    }
}