#nullable enable
using Umbraco.Cms.Core.Models;

namespace Articulate.Models
{
    /// <summary>
    ///     Represents a model with an image.
    /// </summary>
    public interface IImageModel
    {
        /// <summary>
        ///     Gets the image media item.
        /// </summary>
        public MediaWithCrops? Image { get; }

        /// <summary>
        ///     Gets the name of the item.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets the URL of the item.
        /// </summary>
        public string Url { get; }

        /// <summary>
        ///     Gets the wide cropped image URL.
        /// </summary>
        public string CroppedWideUrl { get; }
    }
}
