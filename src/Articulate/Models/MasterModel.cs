using System;
using System.Linq;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Articulate.Models
{
    /// <summary>
    /// The basic model for all articulate objects
    /// </summary>
    public class MasterModel : PublishedContentWrapped, IMasterModel
    {
        public MasterModel(IPublishedContent content, IPublishedValueFallback publishedValueFallback, IVariationContextAccessor variationContextAccessor)
            : base(content, publishedValueFallback)
        {
            PublishedValueFallback = publishedValueFallback;
            VariationContextAccessor = variationContextAccessor;
        }
        
        /// <summary>
        /// Returns the current theme
        /// </summary>
        public string Theme
        {
            get => _theme ??= base.Unwrap().Value<string>("theme", fallback: Fallback.ToAncestors);
            protected set => _theme = value;
        }

        public IPublishedContent RootBlogNode
        {
            get
            {
                var root = base.Unwrap().AncestorOrSelf("Articulate");
                _rootBlogNode = root ?? throw new InvalidOperationException("Could not find the Articulate root document for the current rendered page");
                return _rootBlogNode;
            }
            protected set => _rootBlogNode = value;
        }

        private IPublishedContent _rootBlogNode;
        private string _theme;
        private IPublishedContent _blogListNode;
        private IPublishedContent _blogAuthorsNode;
        private int? _pageSize;
        private string _blogTitle;
        private string _blogDescription;
        private string _blogBanner;
        private string _blogLogo;
        private string _disqusShortName;
        private string _customRssFeed;

        private string _pageTitle;
        private string _pageDescription;

        /// <summary>
        /// This will return the first archive node found under the blog root
        /// </summary>
        /// <remarks>
        /// We can support multiple archive nodes - TODO: Should we change this method to return an array of archive nodes?
        /// </remarks>
        public IPublishedContent BlogArchiveNode
        {
            get
            {
                var list = RootBlogNode.ChildrenOfType(ArticulateConstants.ArticulateArchiveContentTypeAlias).FirstOrDefault();
                _blogListNode = list ?? throw new InvalidOperationException("Could not find the ArticulateArchive document for the current rendered page");
                return _blogListNode;
            }
            protected set => _blogListNode = value;
        }

        /// <summary>
        /// This will return the first archive node found under the blog root
        /// </summary>
        public IPublishedContent BlogAuthorsNode
        {
            get
            {
                var authors = RootBlogNode.ChildrenOfType(ArticulateConstants.ArticulateAuthorsContentTypeAlias).FirstOrDefault();
                _blogAuthorsNode = authors ?? throw new InvalidOperationException("Could not find the ArticulateAuthors document for the current rendered page");
                return _blogAuthorsNode;
            }
            protected set => _blogListNode = value;
        }

        public string DisqusShortName
        {
            get => _disqusShortName ?? (_disqusShortName = base.Unwrap().Value<string>("disqusShortname", fallback: Fallback.ToAncestors));
            protected set => _disqusShortName = value;
        }

        public string CustomRssFeed
        {
            get => _customRssFeed ?? (_customRssFeed = RootBlogNode.Value<string>("customRssFeedUrl"));
            protected set => _customRssFeed = value;
        }        

        public string BlogLogo
        {
            get => _blogLogo ?? (_blogLogo = RootBlogNode.GetArticulateCropUrl("blogLogo", VariationContextAccessor?.VariationContext));
            protected set => _blogLogo = value;
        }

        public string BlogBanner
        {
            get => _blogBanner ?? (_blogBanner = RootBlogNode.GetArticulateCropUrl("blogBanner", VariationContextAccessor?.VariationContext));
            protected set => _blogBanner = value;
        }

        public string BlogTitle
        {
            get => _blogTitle ?? (_blogTitle = base.Unwrap().Value<string>("blogTitle", fallback: Fallback.ToAncestors));
            protected set => _blogTitle = value;
        }

        public string BlogDescription
        {
            get => _blogDescription ?? (_blogDescription = base.Unwrap().Value<string>("blogDescription", fallback: Fallback.ToAncestors));
            protected set => _blogDescription = value;
        }

        public int PageSize
        {
            get
            {
                if (_pageSize.HasValue == false)
                {
                    _pageSize = base.Unwrap().Value<int>("pageSize", fallback: Fallback.To(Fallback.Ancestors, Fallback.DefaultValue), defaultValue: 10);
                }

                return _pageSize.Value;
            }
            protected set => _pageSize = value;
        }

        public string PageTitle
        {
            get => _pageTitle ?? (_pageTitle = Name + " - " + BlogTitle);
            protected set => _pageTitle = value;
        }

        public string PageDescription
        {
            get => _pageDescription ?? (_pageDescription = BlogDescription);
            protected set => _pageDescription = value;
        }

        public string PageTags { get; protected set; }
        public IPublishedValueFallback PublishedValueFallback { get; }
        public IVariationContextAccessor VariationContextAccessor { get; }
    }
}
