#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Articulate.Models.Api
{
    /// <summary>
    ///     Represents the response model for the BlogML post initialization.
    /// </summary>
    /// <remarks>
    ///     This model is used as the response for the BlogML post initialization endpoint.
    ///     It contains the temporary file name used to store the uploaded BlogML file,
    ///     as well as the number of posts detected in the file.
    /// </remarks>
    public class ImportFileResponse
    {
        /// <summary>
        ///     Gets or sets the temporary file name used to store the uploaded BlogML file.
        /// </summary>
        /// <value>
        ///     The temporary file name used to store the uploaded BlogML file.
        /// </value>
        [Required(AllowEmptyStrings = false)]
        public required string TemporaryFileName { get; set; }

        /// <summary>
        ///     Gets or sets the number of posts detected in the BlogML file.
        /// </summary>
        /// <value>
        ///     The number of posts detected in the BlogML file.
        /// </value>
        public int PostCount { get; set; }

        /// <summary>
        ///     Gets or sets the number of external image attachments referenced in the BlogML file.
        /// </summary>
        public int ExternalImageCount { get; set; }

        /// <summary>
        ///     Gets or sets the unique external hosts referenced by image attachments in the BlogML file.
        /// </summary>
        public string[] ExternalHosts { get; set; } = [];

        /// <summary>
        ///     Gets or sets the external hosts referenced by the BlogML file that are not currently allowlisted.
        /// </summary>
        public string[] BlockedExternalHosts { get; set; } = [];
    }
}
