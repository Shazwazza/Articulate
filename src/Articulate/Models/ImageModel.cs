using Umbraco.Core.PropertyEditors.ValueConverters;

namespace Articulate.Models
{
    public interface IImageModel
    {
        ImageCropperValue Image { get; }
        string Name { get; }
        string Url { get; }
    }
}