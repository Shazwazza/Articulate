#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Articulate.Models.Api
{
    /// <summary>
    ///     Represents the model for creating a new post from the Markdown editor.
    /// </summary>
    public class MarkdownEditorModel
    {
        /// <summary>
        ///     The Articulate blog node ID.
        /// </summary>
        [Required]
        public required int ArticulateBlogNode { get; set; }

        /// <summary>
        ///     The post title.
        /// </summary>
        [Required]
        public required string Title { get; set; }

        /// <summary>
        ///     The post body in Markdown format.
        /// </summary>
        [Required]
        public required string Body { get; set; }

        /// <summary>
        ///     Comma-separated tags.
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        ///     Comma-separated categories.
        /// </summary>
        public string? Categories { get; set; }

        /// <summary>
        ///     The post slug.
        /// </summary>
        public string? Slug { get; set; }

        /// <summary>
        ///     The post excerpt.
        /// </summary>
        public string? Excerpt { get; set; }
    }
}
