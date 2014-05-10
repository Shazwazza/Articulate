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
        private readonly string _pageTypeAlias;
        private readonly string _urlPath;

        public ArticulateVirtualPage(IPublishedContent rootBlogPage, string pageName, string pageTypeAlias)
            : base(rootBlogPage)
        {
            _pageName = pageName;
            _pageTypeAlias = pageTypeAlias;
        }

        public ArticulateVirtualPage(IPublishedContent rootBlogPage, string pageName, string pageTypeAlias, string urlPath)
            : base(rootBlogPage)
        {
            _pageName = pageName;
            _pageTypeAlias = pageTypeAlias;
            _urlPath = urlPath;
        }

        public override string Url
        {
            get { return base.Url.EnsureEndsWith('/') + (_urlPath ?? UrlName); }
        }

        public override PublishedContentType ContentType
        {
            get { return null; }
        }

        public override IPublishedContent Parent
        {
            get { return Content; }
        }

        public override int Id
        {
            get { return int.MaxValue; }
        }

        public override string Name
        {
            get { return _pageName; }
        }

        public override string UrlName
        {
            get { return _pageName.ToLowerInvariant(); }
        }

        public override string DocumentTypeAlias
        {
            get { return _pageTypeAlias; }
        }

        public override int DocumentTypeId
        {
            get { return int.MaxValue; }
        }

        public override string Path
        {
            get { return Content.Path.EnsureEndsWith(',') + Id; }
        }

        public override int Level
        {
            get { return Content.Level + 1; }
        }
    }
}