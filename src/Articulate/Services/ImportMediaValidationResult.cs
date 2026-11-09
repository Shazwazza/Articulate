#nullable enable

namespace Articulate.Services
{
    /// <summary>
    /// Represents the result of an image validation operation.
    /// </summary>
    public class ImportMediaValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the image is valid.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// The validated image stream. Caller is responsible for disposing this stream.
        /// </summary>
        public Stream? ValidatedStream { get; init; }

        /// <summary>
        /// The correct file extension.
        /// </summary>
        public string? CorrectExtension { get; init; }

        /// <summary>
        /// The MIME type based on file extension (e.g., "image/jpeg", "image/png").
        /// </summary>
        public string? MimeType { get; init; }

        /// <summary>
        /// Any error message if validation failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Creates a successful validation result.
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
        /// Creates a failed validation result.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>A failed validation result.</returns>
        public static ImportMediaValidationResult Failure(string errorMessage) =>
            new() { IsValid = false, ErrorMessage = errorMessage };
    }
}
