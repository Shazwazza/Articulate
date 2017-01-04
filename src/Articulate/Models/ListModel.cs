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
        private PostModel[] _resolvedList;
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
            if(content.DocumentTypeAlias.Equals("ArticulateArchive"))
                PageTitle = BlogTitle + " - " + BlogDescription;
            else
                PageTags = Name;
        }
        
        /// <summary>
        /// The pager model
        /// </summary>
        public PagerModel Pages => _pager;

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

                _resolvedList = _listItems           
                    //We'll skip take here just in case but the list passed to the ctor should ideally already be filtered         
                    .Skip(_pager.CurrentPageIndex * _pager.PageSize)
                    .Take(_pager.PageSize)
                    .Select(x => new PostModel(x))
                    .ToArray();

                return _resolvedList;
            }
        }
    }
}