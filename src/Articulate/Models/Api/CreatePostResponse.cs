#nullable enable
namespace Articulate.Models.Api
{
    /// <summary>
    ///     Represents the response of creating a post.
    /// </summary>
    /// <remarks>
    ///     The response contains the URL of the created post.
    /// </remarks>
    public class CreatePostResponse
    {
        /// <summary>
        ///     The URL of the created post.
        /// </summary>
        public required string Url { get; set; }
    }
}
