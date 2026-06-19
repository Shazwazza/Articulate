#nullable enable
using System.Collections.Frozen;
using OpenIddict.Abstractions;

namespace Articulate.Options
{
    /// <summary>
    ///     Configuration options for registering a custom OpenIddict client used by the Articulate management extensions.
    /// </summary>
    public sealed class ArticulateOpenIdClientOptions
    {
        /// <summary>
        ///     The configuration section that maps to <see cref="ArticulateOpenIdClientOptions" />.
        /// </summary>
        public const string SectionName = "Articulate:ManagementApi:OpenIddict:Client";

        private static readonly FrozenSet<string> _defaultPermissions = FrozenSet.ToFrozenSet([
            // Allow the client to initiate the authorization code flow.
            OpenIddictConstants.Permissions.Endpoints.Authorization,

            // Allow the client to exchange the authorization code for tokens.
            OpenIddictConstants.Permissions.Endpoints.Token,

            // Allow the client to trigger the end-session (sign-out) endpoint.
            OpenIddictConstants.Permissions.Endpoints.EndSession,

            // Allow the client to revoke its own tokens via the revocation endpoint.
            OpenIddictConstants.Permissions.Endpoints.Revocation,

            // Constrain the client to the authorization code grant type.
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,

            // Constrain the authorization response to "code" only (no implicit/hybrid).
            OpenIddictConstants.Permissions.ResponseTypes.Code
        ]);

        private static readonly FrozenSet<string> _defaultRequirements = FrozenSet.ToFrozenSet([
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
        ]);

        /// <summary>
        ///     Gets or sets a value indicating whether the custom client registration is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        ///     Gets or sets the client identifier for the custom application.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        ///     Gets or sets the display name shown for the client application.
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        ///     Gets or sets the client type to register with OpenIddict. Defaults to
        ///     <see cref="OpenIddictConstants.ClientTypes.Public" />.
        /// </summary>
        public string ClientType { get; set; } = OpenIddictConstants.ClientTypes.Public;

        /// <summary>
        ///     Gets or sets the client secret used for confidential clients.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        ///     Gets or sets the allowed callback URLs after a successful sign-in. When <see cref="Enabled" /> is
        ///     <see langword="true" />, at least one absolute URI must be provided.
        /// </summary>
        public List<string> RedirectUris { get; set; } = [];

        /// <summary>
        ///     Gets or sets the allowed final destinations after sign-out completes.
        /// </summary>
        public List<string> PostLogoutRedirectUris { get; set; } = [];

        /// <summary>
        ///     Gets or sets the custom Authorization URL.
        /// </summary>
        public string? AuthorizeUrl { get; set; }

        /// <summary>
        ///     Gets or sets the custom Token URL.
        /// </summary>
        public string? TokenUrl { get; set; }

        /// <summary>
        ///     Gets or sets the custom End Session URL.
        /// </summary>
        public string? EndSessionUrl { get; set; }

        /// <summary>
        ///     Gets or sets the custom Revocation URL.
        /// </summary>
        public string? RevocationUrl { get; set; }

        /// <summary>
        ///     Gets or sets the custom Current User URL.
        /// </summary>
        public string? CurrentUserUrl { get; set; }

        /// <summary>
        ///     Gets or sets the custom Login Logo URL.
        /// </summary>
        public string? LoginLogoUrl { get; set; }

        /// <summary>
        ///     Gets the permissions granted to the custom client.
        ///     Defaults restrict the client to the authorization code flow with PKCE
        ///     and explicitly scope which OpenIddict endpoints it can call.
        /// </summary>
        public IReadOnlyCollection<string> Permissions => _defaultPermissions;

        /// <summary>
        ///     Gets the requirements enforced for the custom client. Defaults enforce PKCE support.
        /// </summary>
        public IReadOnlyCollection<string> Requirements => _defaultRequirements;

        /// <summary>
        ///     Determines whether a non-empty client secret is configured.
        /// </summary>
        public bool HasClientSecret() => !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
