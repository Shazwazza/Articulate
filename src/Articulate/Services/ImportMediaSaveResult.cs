#nullable enable
using Umbraco.Cms.Core.Models;

namespace Articulate.Services
{
    /// <summary>
    /// Represents the result of an image save operation.
    /// </summary>
    public class ImportMediaSaveResult
    {
        /// <summary>
        /// Gets a value indicating whether the save was successful.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// The saved media item.
        /// </summary>
        public IMedia? Media { get; init; }

        /// <summary>
        /// The UDI of the saved media item.
        /// </summary>
        public string? MediaUdi { get; init; }

        /// <summary>
        /// Any error message if saving failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Creates a successful save result.
        /// </summary>
        /// <param name="media">The saved media item.</param>
        /// <param name="mediaUdi">The UDI of the media item.</param>
        /// <returns>A successful save result.</returns>
        public static ImportMediaSaveResult Succeeded(IMedia media, string mediaUdi) =>
            new() { Success = true, Media = media, MediaUdi = mediaUdi };

        /// <summary>
        /// Creates a failed save result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed save result.</returns>
        public static ImportMediaSaveResult Failed(string errorMessage) =>
            new() { Success = false, ErrorMessage = errorMessage };
    }
}
