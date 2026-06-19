#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Articulate.Models.Api
{
    /// <summary>
    ///     Represents the options for exporting blog data in BlogML format.
    /// </summary>
    /// <remarks>
    ///     This model is used as the request body for the BlogML export endpoint.
    ///     It contains the unique identifier of the Articulate node to export,
    ///     as well as an option to export images as Base64 strings within the BlogML XML file.
    /// </remarks>
    public class ExportModel
    {
        /// <summary>
        ///     Gets or sets the unique identifier of the Articulate node to export.
        /// </summary>
        /// <remarks>
        ///     This is the identifier of the root node of the blog you want to export.
        /// </remarks>
        [Required]
        public Guid ArticulateBlogNode { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the images should be exported as Base64 strings within the BlogML XML file.
        /// </summary>
        /// <remarks>
        ///     When set to <c>true</c>, the images will be exported as Base64 strings within the BlogML XML file.
        ///     Otherwise, the original URLs will be used.
        /// </remarks>
        public bool ExportImagesAsBase64 { get; set; } = false;
    }
}
