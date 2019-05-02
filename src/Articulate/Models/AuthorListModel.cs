using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Articulate.Models
{
    public class AuthorListModel : MasterModel
    {
        public AuthorListModel(IPublishedContent content)
            : base(content)
        {
        }

        public IEnumerable<AuthorModel> Authors { get; set; }
    }
}