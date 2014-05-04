using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Articulate.Models
{
    public class ListModel : BlogModel
    {
        private readonly IEnumerable<IPublishedContent> _listItems;

        public ListModel(IPublishedContent content, IEnumerable<IPublishedContent> listItems)
            : base(content)
        {
            _listItems = listItems;
        }

        public ListModel(IPublishedContent content)
            : base(content)
        {
            _listItems = base.Children;
        }

        public override IEnumerable<IPublishedContent> Children
        {
            get { return _listItems.Select(x => new PostModel(x)); }
        }
    }
}