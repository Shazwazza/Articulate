#nullable enable
using Umbraco.Cms.Core.Models;

namespace Articulate.Services
{
    /// <summary>
    ///     Service for processing and validating images across Articulate features (BlogML import, MetaWeblog, Markdown
    ///     editor).
    ///     Centralizes image validation, processing, and storage logic.
    /// </summary>
    public interface IArticulateImportMediaService
    {
        /// <summary>
        ///     Validates an image stream (extension matching only).
        /// </summary>
        /// <param name="stream">The image stream to validate.</param>
        /// <param name="originalExtension">The original file extension.</param>
        /// <returns>A validation result containing the stream and metadata on success.</returns>
        public ValueTask<ImportMediaValidationResult> ValidateImageAsync(Stream stream, string originalExtension);

        /// <summary>
        ///     Decodes and validates a base64-encoded image, capped by Articulate:MaxImportImageBytes.
        /// </summary>
        /// <param name="base64Content">The base64-encoded image content.</param>
        /// <param name="originalFileName">The original filename.</param>
        /// <returns>A validation result containing the decoded stream and metadata on success.</returns>
        public Task<ImportMediaValidationResult> DecodeAndValidateBase64ImageAsync(
            string base64Content,
            string originalFileName);

        /// <summary>
        ///     Downloads and validates an image from an external URL.
        /// </summary>
        /// <param name="imageUrl">The URL of the external image.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A validation result containing the downloaded stream and metadata on success.</returns>
        public Task<ImportMediaValidationResult> DownloadAndValidateImageAsync(
            Uri imageUrl,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Saves a validated image to the Umbraco media library.
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
        ///     Saves a validated image to the Articulate file system (articulate/{guid}/{filename}).
        /// </summary>
        /// <param name="imageStream">The image stream to save.</param>
        /// <param name="extension">The file extension.</param>
        /// <param name="originalFileName">The optional original filename to use as a basis.</param>
        /// <returns>The absolute URL to the saved image, or empty string on failure.</returns>
        public string SaveToFileSystem(Stream imageStream, string extension, string? originalFileName = null);

        /// <summary>
        ///     Gets or creates the Articulate media folder in the media library.
        /// </summary>
        /// <returns>The media item representing the Articulate folder.</returns>
        public IMedia GetOrCreateArticulateMediaFolder();
    }

    /// <summary>
    ///     Represents the result of an image validation operation.
    /// </summary>
    public class ImportMediaValidationResult
    {
        /// <summary>
        ///     Gets a value indicating whether the image is valid.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        ///     The validated image stream. Caller is responsible for disposing this stream.
        /// </summary>
        public Stream? ValidatedStream { get; init; }

        /// <summary>
        ///     The correct file extension.
        /// </summary>
        public string? CorrectExtension { get; init; }

        /// <summary>
        ///     The MIME type based on file extension (e.g., "image/jpeg", "image/png").
        /// </summary>
        public string? MimeType { get; init; }

        /// <summary>
        ///     Any error message if validation failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        ///     Creates a successful validation result.
        /// </summary>
        /// <param name="stream">The image stream.</param>
        /// <param name="correctExtension">The correct extension.</param>
        /// <param name="mimeType">The MIME type.</param>
        /// <returns>A successful validation result.</returns>
        public static ImportMediaValidationResult Success(Stream stream, string correctExtension, string mimeType) =>
            new()
            {
                IsValid = true, ValidatedStream = stream, CorrectExtension = correctExtension, MimeType = mimeType
            };

        /// <summary>
        ///     Creates a failed validation result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed validation result.</returns>
        public static ImportMediaValidationResult Failure(string errorMessage) =>
            new() { IsValid = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    ///     Represents the result of an image save operation.
    /// </summary>
    public class ImportMediaSaveResult
    {
        /// <summary>
        ///     Gets a value indicating whether the save was successful.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        ///     The saved media item.
        /// </summary>
        public IMedia? Media { get; init; }

        /// <summary>
        ///     The UDI of the saved media item.
        /// </summary>
        public string? MediaUdi { get; init; }

        /// <summary>
        ///     Any error message if saving failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        ///     Creates a successful save result.
        /// </summary>
        /// <param name="media">The saved media item.</param>
        /// <param name="mediaUdi">The UDI of the media item.</param>
        /// <returns>A successful save result.</returns>
        public static ImportMediaSaveResult Succeeded(IMedia media, string mediaUdi) =>
            new() { Success = true, Media = media, MediaUdi = mediaUdi };

        /// <summary>
        ///     Creates a failed save result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed save result.</returns>
        public static ImportMediaSaveResult Failed(string errorMessage) =>
            new() { Success = false, ErrorMessage = errorMessage };
    }
}
