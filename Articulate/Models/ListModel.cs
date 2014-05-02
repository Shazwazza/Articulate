using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Articulate.Models
{
    public class ListModel : BlogModel
    {
        public ListModel(IPublishedContent content)
            : base(content)
        {
        }

        public override IEnumerable<IPublishedContent> Children
        {
            get { return base.Children.Select(x => new PostModel(x)); }
        }
    }
}