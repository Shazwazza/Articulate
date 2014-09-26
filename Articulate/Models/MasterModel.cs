using System;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public abstract class MasterModel : PublishedContentWrapped, IMasterModel
    {
        protected MasterModel(IPublishedContent content)
            : base(content)
        {
        }
        
        /// <summary>
        /// Returns the current theme
        /// </summary>
        public string Theme
        {
            get { return _theme ?? (_theme = Content.GetPropertyValue<string>("theme", true)); }
            protected set { _theme = value; }
        }

        public IPublishedContent RootBlogNode
        {
            get
            {
                var root = Content.AncestorOrSelf("Articulate");
                if (root == null)
                {
                    throw new InvalidOperationException("Could not find the Articulate root document for the current rendered page");
                }
                _rootBlogNode = root;
                return _rootBlogNode;
            }
            protected set { _rootBlogNode = value; }
        }

        private IPublishedContent _rootBlogNode;
        private string _theme;
        private IPublishedContent _blogListNode;
        private int? _pageSize;
        private string _blogTitle;
        private string _blogDescription;
        private string _blogBanner;
        private string _blogLogo;
        private string _disqusShortName;
        private string _customRssFeed;

        public IPublishedContent BlogArchiveNode
        {
            get
            {
                var list = RootBlogNode.Children(content => content.DocumentTypeAlias.InvariantEquals("ArticulateArchive")).FirstOrDefault();
                if (list == null)
                {
                    throw new InvalidOperationException("Could not find the ArticulateArchive document for the current rendered page");
                }
                _blogListNode = list;
                return _blogListNode;
            }
            protected set { _blogListNode = value; }
        }

        public string DisqusShortName
        {
            get { return _disqusShortName ?? (_disqusShortName = Content.GetPropertyValue<string>("disqusShortname", true)); }
            protected set { _disqusShortName = value; }
        }

        public string CustomRssFeed
        {
            get { return _customRssFeed ?? (_customRssFeed = RootBlogNode.GetPropertyValue<string>("customRssFeedUrl")); }
            protected set { _customRssFeed = value; }
        }

        public string BlogLogo
        {
            get { return _blogLogo ?? (_blogLogo = RootBlogNode.GetCropUrl(propertyAlias: "blogLogo", imageCropMode: ImageCropMode.Max)); }
            protected set { _blogLogo = value; }
        }

        public string BlogBanner
        {
            get { return _blogBanner ?? (_blogBanner = RootBlogNode.GetCropUrl(propertyAlias: "blogBanner", imageCropMode: ImageCropMode.Max)); }
            protected set { _blogBanner = value; }
        }

        public string BlogTitle
        {
            get { return _blogTitle ?? (_blogTitle = Content.GetPropertyValue<string>("blogTitle", true)); }
            protected set { _blogTitle = value; }
        }

        public string BlogDescription
        {
            get { return _blogDescription ?? (_blogDescription = Content.GetPropertyValue<string>("blogDescription", true)); }
            protected set { _blogDescription = value; }
        }

        public int PageSize
        {
            get
            {
                if (_pageSize.HasValue == false)
                {
                    _pageSize = Content.GetPropertyValue<int>("pageSize", 10);
                }
                return _pageSize.Value;
            }
            protected set { _pageSize = value; }
        }
    }
}