// Nullable reference annotations are used for the optional fields exposed to the view.

#nullable enable
namespace Articulate.Models
{
    /// <summary>
    /// Model to initialize the Markdown editor with security and endpoint configuration.
    /// </summary>
    public class MarkdownEditorInitModel
    {
        public required string AuthorizeUrl { get; init; }

        public required string CurrentUserUrl { get; init; }

        public required string EndSessionUrl { get; init; }

        public required string TokenUrl { get; init; }

        public required string RevocationUrl { get; init; }

        public required string LoginLogoUrl { get; init; }

        public required int ArticulateBlogNode { get; init; }

        public required string EditorPostUrl { get; init; }

        public required string BackOfficeClientId { get; init; }

        public required string PostLogoutRedirectUrl { get; init; }

        /// <summary>
        /// Gets the back office external login provider to send as <c>identity_provider</c>, or
        /// <see langword="null"/> to use local back office login.
        /// </summary>
        public string? IdentityProvider { get; init; }

    }
}
