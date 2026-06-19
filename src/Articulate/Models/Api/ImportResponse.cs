namespace Articulate.Models.Api
{
    /// <summary>
    ///     Represents the result of a BlogML import with operation statistics.
    /// </summary>
    /// <remarks>
    ///     The response contains the number of posts, authors, and comments imported.
    ///     It also contains a boolean indicating whether the import completed successfully
    ///     or if it failed.
    /// </remarks>
    public class ImportResponse
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ImportResponse" /> class.
        /// </summary>
        public ImportResponse()
        {
        }

        internal ImportResponse(ImportResponseDto dto)
        {
            AuthorCount = dto.AuthorCount;
            CommentCount = dto.CommentCount;
            Completed = dto.Completed;
            PostCount = dto.PostCount;
        }

        /// <summary>
        ///     Gets or sets the number of Posts that were imported.
        /// </summary>
        public long PostCount { get; set; }

        /// <summary>
        ///     Gets or sets the number of Authors that were imported.
        /// </summary>
        public long AuthorCount { get; set; }

        /// <summary>
        ///     Gets or sets the number of comments that were written to the Disqus comments XML export file.
        /// </summary>
        public long CommentCount { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the import completed successfully (true) or if it failed (false).
        /// </summary>
        /// <value>
        ///     <c>true</c> if the import completed successfully; otherwise, <c>false</c>.
        /// </value>
        public bool Completed { get; set; }
    }
}
