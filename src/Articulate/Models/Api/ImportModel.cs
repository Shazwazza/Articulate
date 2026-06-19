#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Articulate.Models.Api
{
    /// <summary>
    ///     Represents the options for importing blog data from a BlogML file.
    /// </summary>
    /// <remarks>
    ///     This model is used as the request body for the BlogML post import endpoint.
    ///     It contains the unique identifier of the Articulate node to import into,
    ///     as well as various options for the import process such as whether to
    ///     overwrite existing posts, apply regular expression matches and replacements,
    ///     publish imported posts, export Disqus XML after import, and import the
    ///     first image found in each post.
    /// </remarks>
    public class ImportModel
    {
        /// <summary>
        ///     Gets or sets the unique identifier of the Articulate node to import into.
        /// </summary>
        /// <remarks>
        ///     This property is required and must be set to a valid unique identifier
        ///     of an Articulate blog node.
        /// </remarks>
        [Required]
        public Guid ArticulateBlogNode { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to overwrite existing posts during import.
        /// </summary>
        /// <remarks>
        ///     This property is optional and defaults to <see langword="false" />. If set to
        ///     <see langword="true" />, existing posts will be overwritten with the imported
        ///     data. Otherwise, existing posts with the same identifier as the imported
        ///     posts will be skipped and not overwritten.
        /// </remarks>
        public bool Overwrite { get; set; }

        /// <summary>
        ///     Gets or sets the regular expression pattern to match in post content during import.
        /// </summary>
        /// <remarks>
        ///     This property is optional and can be set to a regular expression pattern to
        ///     match in post content during import. If set, the regular expression pattern
        ///     will be used to match content in each post, and the matched content will be
        ///     replaced with the replacement string set in the <see cref="RegexReplace" />
        ///     property.
        /// </remarks>
        public string? RegexMatch { get; set; }

        /// <summary>
        ///     Gets or sets the replacement string for the regular expression match in post content.
        /// </summary>
        /// <remarks>
        ///     This property is optional and can be set to a replacement string to replace
        ///     the matched content in each post. If set, it will be used in conjunction with
        ///     the <see cref="RegexMatch" /> property to replace the matched content in
        ///     each post.
        /// </remarks>
        public string? RegexReplace { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to publish imported posts.
        /// </summary>
        /// <remarks>
        ///     If set to <see langword="true" />, posts will be published after import. If set to
        ///     <see langword="false" />, posts will not be published after import.
        /// </remarks>
        public bool Publish { get; set; }

        /// <summary>
        ///     Gets or sets the temporary file name of the uploaded BlogML file.
        /// </summary>
        /// <remarks>
        ///     This property is required and must be set to a valid file name. The file name
        ///     should be the name of the temporary file that was uploaded to the server
        ///     during the BlogML import initialization process.
        /// </remarks>
        [Required(AllowEmptyStrings = false)]
        public required string TempFile { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to export Disqus XML after import.
        /// </summary>
        /// <remarks>
        ///     If set to <see langword="true" />, Disqus XML will be exported after import.
        ///     If set to <see langword="false" />, Disqus XML will not be exported after import.
        /// </remarks>
        public bool ExportDisqusXml { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to import the first image found in each post.
        /// </summary>
        public bool ImportFirstImage { get; set; }
    }
}
