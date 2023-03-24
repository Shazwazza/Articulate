using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

namespace Articulate.Models
{
    public class PostAuthorModel
    {
        public string Name { get; set; }

        public string Bio { get; set; }

        public string Url { get; set; }
        
        public MediaWithCrops Image { get; set; }

        public string BlogUrl { get; set; }
    }
}
