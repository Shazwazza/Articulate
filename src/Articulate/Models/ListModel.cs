using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Articulate.Models
{
    /// <summary>
    /// Represents a page that displays a list of blog posts
    /// </summary>
    public class ListModel : MasterModel
    {
        private readonly IEnumerable<IPublishedContent> _listItems;
        private IEnumerable<PostModel> _resolvedList;
        private readonly PagerModel _pager;

        /// <summary>
        /// Constructor accepting an explicit list of child items
        /// </summary>
        /// <param name="content"></param>
        /// <param name="listItems"></param>
        /// <param name="pager"></param>
        /// <remarks>
        /// Default sorting by published date will be disabled for this list model, it is assumed that the list items will
        /// already be sorted.
        /// </remarks>
        public ListModel(IPublishedContent content, IEnumerable<IPublishedContent> listItems, PagerModel pager)
            : base(content)
        {
            
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (listItems == null) throw new ArgumentNullException(nameof(listItems));
            if (pager == null) throw new ArgumentNullException(nameof(pager));

            _pager = pager;
            _listItems = listItems;
            if (content.ContentType.Alias.Equals(ArticulateConstants.ArticulateArchiveContentTypeAlias))
                PageTitle = BlogTitle + " - " + BlogDescription;
            else
                PageTags = Name;
        }        

        public ListModel(IPublishedContent content)
            : base(content)
        {
        }
        
        /// <summary>
        /// The pager model
        /// </summary>
        public PagerModel Pages => _pager;

        /// <summary>
        /// Strongly typed access to the list of blog posts
        /// </summary>
        public IEnumerable<PostModel> Posts
        {
            get
            {

                if (_resolvedList != null)
                    return _resolvedList;

                if (_listItems == null)
                {
                    _resolvedList = base.ChildrenForAllCultures.Select(x => new PostModel(x)).ToArray();
                    return _resolvedList;
                }


                if (_listItems != null && _pager != null)
                {
                    _resolvedList = _listItems
                    //Skip will already be done in this case, but we'll take again anyways just to be safe                    
                        .Take(_pager.PageSize)
                        .Select(x => new PostModel(x))
                        .ToArray();
                }
                else
                {
                    _resolvedList = Enumerable.Empty<PostModel>();
                }

                return _resolvedList;
            }
        }

        /// <summary>
        /// The list of blog posts
        /// </summary>
        public override IEnumerable<IPublishedContent> Children => Posts;

        public override IEnumerable<IPublishedContent> ChildrenForAllCultures => Posts;
    }
}
