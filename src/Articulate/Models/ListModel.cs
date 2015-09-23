using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

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
        private readonly bool _disableSort = false;

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
            if (listItems == null) throw new ArgumentNullException("listItems");
            if (pager == null) throw new ArgumentNullException("pager");            
            _pager = pager;
            _disableSort = true;
            _listItems = listItems;
        }

        public ListModel(IPublishedContent content, PagerModel pager)
            : base(content)
        {
            if (pager == null) throw new ArgumentNullException("pager");
            _pager = pager;
        }

        public ListModel(IPublishedContent content)
            : base(content)
        {
            _listItems = base.Children;
        }

        /// <summary>
        /// The pager model
        /// </summary>
        public PagerModel Pages
        {
            get
            {
                if (_pager == null)
                {
                    throw new NullReferenceException("The constructor to set the pager was not used for this instance");
                }
                return _pager;
            }
        }

        /// <summary>
        /// The list of blog posts
        /// </summary>
        public override IEnumerable<IPublishedContent> Children
        {
            get
            {
                if (_resolvedList != null)
                {
                    return _resolvedList;
                }

                if (_listItems == null)
                {
                    //we'll get the result from the base children
                    _resolvedList = base.Children
                        .Select(x => new PostModel(x));
                    if (!_disableSort)
                    {
                        _resolvedList = _resolvedList.OrderByDescending(x => x.PublishedDate);
                    }
                    _resolvedList = _resolvedList.Skip(_pager.CurrentPageIndex*_pager.PageSize).Take(_pager.PageSize).ToArray();
                }
                else
                {
                    _resolvedList = _listItems
                        .Select(x => new PostModel(x));
                    if (!_disableSort)
                    {
                        _resolvedList = _resolvedList.OrderByDescending(x => x.PublishedDate);
                    }
                    _resolvedList = _resolvedList.Skip(_pager.CurrentPageIndex*_pager.PageSize).Take(_pager.PageSize).ToArray();
                }

                return _resolvedList;
            }
        }
    }
}