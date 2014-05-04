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

        public IPublishedContent BlogListNode
        {
            get
            {
                var list = RootBlogNode.Children(content => content.DocumentTypeAlias.InvariantEquals("ArticulateList")).FirstOrDefault();
                if (list == null)
                {
                    throw new InvalidOperationException("Could not find the ArticulateList document for the current rendered page");
                }
                _blogListNode = list;
                return _blogListNode;
            }
        }

        public abstract string BlogTitle { get; }
        public abstract string BlogDescription { get; }
        public abstract string RootUrl { get; }
        public abstract string ArchiveUrl { get; }
    }
}