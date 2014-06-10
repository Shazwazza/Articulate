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
            get { return Content.GetPropertyValue<string>("theme", true); }
        }

        private IPublishedContent _rootBlogNode;

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
        }

        private IPublishedContent _blogListNode;

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
        }

        public string BlogLogo
        {
            get { return RootBlogNode.GetCropUrl(propertyAlias: "blogLogo", imageCropMode: ImageCropMode.Max); }
        }

        public string BlogBanner
        {
            get { return RootBlogNode.GetCropUrl(propertyAlias: "blogBanner", imageCropMode: ImageCropMode.Max); }
        }

        public string BlogTitle
        {
            get { return Content.GetPropertyValue<string>("blogTitle", true); }
        }

        public string BlogDescription
        {
            get { return Content.GetPropertyValue<string>("blogDescription", true); }
        }

        public int PageSize
        {
            get { return Content.GetPropertyValue<int>("pageSize", 10); }
        }
    }
}