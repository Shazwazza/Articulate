#nullable enable
using Umbraco.Cms.Core.Models;

namespace Articulate.Services
{
    /// <summary>
    /// Service for processing and validating images across Articulate features (BlogML import, MetaWeblog, Markdown editor).
    /// Centralizes image validation, processing, and storage logic.
    /// </summary>
    public interface IArticulateImportMediaService
    {
        /// <summary>
        /// Validates an image stream (extension matching only).
        /// </summary>
        /// <param name="stream">The image stream to validate.</param>
        /// <param name="originalExtension">The original file extension.</param>
        /// <returns>A validation result containing the stream and metadata on success.</returns>
        public ValueTask<ImportMediaValidationResult> ValidateImageAsync(Stream stream, string originalExtension);

        /// <summary>
        /// Decodes and validates a base64-encoded image, capped by Articulate:MaxImportImageBytes.
        /// </summary>
        /// <param name="base64Content">The base64-encoded image content.</param>
        /// <param name="originalFileName">The original filename.</param>
        /// <returns>A validation result containing the decoded stream and metadata on success.</returns>
        public Task<ImportMediaValidationResult> DecodeAndValidateBase64ImageAsync(
            string base64Content,
            string originalFileName);

        /// <summary>
        /// Downloads and validates an image from an external URL.
        /// </summary>
        /// <param name="imageUrl">The URL of the external image.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A validation result containing the downloaded stream and metadata on success.</returns>
        public Task<ImportMediaValidationResult> DownloadAndValidateImageAsync(
            Uri imageUrl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a validated image to the Umbraco media library.
        /// </summary>
        /// <param name="imageStream">The image stream to save.</param>
        /// <param name="mediaName">The name for the media item.</param>
        /// <param name="extension">The file extension.</param>
        /// <param name="parentFolder">The optional parent folder in the media library.</param>
        /// <returns>A save result containing the media item and its UDI on success.</returns>
        public ImportMediaSaveResult SaveToMediaLibrary(
            Stream imageStream,
            string mediaName,
            string extension,
            IMedia? parentFolder = null);

        /// <summary>
        /// Saves a validated image to the Articulate file system (articulate/{guid}/{filename}).
        /// </summary>
        /// <param name="imageStream">The image stream to save.</param>
        /// <param name="extension">The file extension.</param>
        /// <param name="originalFileName">The optional original filename to use as a basis.</param>
        /// <returns>The absolute URL to the saved image, or empty string on failure.</returns>
        public string SaveToFileSystem(Stream imageStream, string extension, string? originalFileName = null);

        /// <summary>
        /// Gets or creates the Articulate media folder in the media library.
        /// </summary>
        /// <returns>The media item representing the Articulate folder.</returns>
        public IMedia GetOrCreateArticulateMediaFolder();
    }
}
