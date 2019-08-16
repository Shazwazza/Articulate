using System;
using System.Collections.Generic;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate.Models
{
    /// <summary>
    /// Used to create a fake dynamic umbraco page for rendering tag lists, tag pages and search results (any virtual route)
    /// </summary>
    internal class ArticulateVirtualPage : PublishedContentWrapped
    {
        private readonly string _pageName;
        private readonly string _pageTypeAlias;
        private readonly string _urlPath;

        public ArticulateVirtualPage(IPublishedContent rootBlogPage, string pageName, string pageTypeAlias, string urlPath = null)
            : base(rootBlogPage)
        {
            if (pageName == null) throw new ArgumentNullException("pageName");
            if (pageTypeAlias == null) throw new ArgumentNullException("pageTypeAlias");
            _pageName = pageName;
            _pageTypeAlias = pageTypeAlias;

            if (urlPath != null)
            {
                _urlPath = urlPath.SafeEncodeUrlSegments();
            }
            
        }
        
        public override string Url => base.Url.EnsureEndsWith('/') + (_urlPath ?? base.UrlSegment);

        /// <summary>
        /// Returns the content that was used to create this virtual node - we'll assume this virtual node's parent is based on the real node that created it
        /// </summary>
        public override IPublishedContent Parent => base.Unwrap();

        public override int Id => int.MaxValue - Parent.Id;

        public override string Name => _pageName;

        public override string UrlSegment => _pageName.ToLowerInvariant();

        public override IPublishedContentType ContentType
        {
            get
            {
                return new PublishedContentType(int.MaxValue - Parent.ContentType.Id,
                    _pageTypeAlias,
                    base.ContentType.ItemType, 
                    base.ContentType.CompositionAliases,
                    x => base.ContentType.PropertyTypes,
                    base.ContentType.Variations);
            }
        }        

        public override string Path => base.Unwrap().Path.EnsureEndsWith(',') + Id;

        public override int Level => base.Unwrap().Level + 1;
    }
}
