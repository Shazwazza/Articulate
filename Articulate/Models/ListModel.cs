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
        private readonly PagerModel _pager;

        public ListModel(IPublishedContent content, IEnumerable<IPublishedContent> listItems, PagerModel pager)
            : base(content)
        {
            if (listItems == null) throw new ArgumentNullException("listItems");
            if (pager == null) throw new ArgumentNullException("pager");            
            _pager = pager;

            //Create the list items based on the pager details
            _listItems = listItems.Skip(_pager.CurrentPageIndex * _pager.PageSize).Take(_pager.PageSize).ToArray();
        }

        public ListModel(IPublishedContent content, PagerModel pager)
            : base(content)
        {
            if (pager == null) throw new ArgumentNullException("pager");
            _pager = pager;

            //Create the list items based on the pager details
            _listItems = base.Children.Skip(_pager.CurrentPageIndex*_pager.PageSize).Take(_pager.PageSize).ToArray();
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
                return _listItems.Select(x => new PostModel(x))
                    .OrderByDescending(x => x.PublishedDate);
            }
        }
    }
}