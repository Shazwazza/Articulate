using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

namespace Articulate.Models
{
    public interface IImageModel
    {
        MediaWithCrops Image { get; }
        string Name { get; }
        string Url { get; }
    }
}
