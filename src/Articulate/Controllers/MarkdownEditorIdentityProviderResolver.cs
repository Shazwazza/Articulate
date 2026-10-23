#nullable enable

namespace Articulate.Controllers
{
    /// <summary>
    /// Resolves the back office external login provider the standalone Markdown editor should sign in with.
    /// </summary>
    /// <remarks>
    /// The resolved value is sent as the OpenIddict <c>identity_provider</c> parameter on the authorize request, which
    /// makes Umbraco challenge that external provider directly instead of showing the local back office login screen.
    /// This is the same mechanism the Umbraco back office client uses.
    /// </remarks>
    internal static class MarkdownEditorIdentityProviderResolver
    {
        /// <summary>
        /// The identity provider name Umbraco uses for local (username/password) back office login.
        /// </summary>
        public const string LocalProvider = "Umbraco";

        /// <summary>
        /// The back office external login scheme registered by Umbraco Cloud for Umbraco ID.
        /// </summary>
        /// <remarks>
        /// Umbraco Cloud hides the local login option in the back office client rather than by setting
        /// <c>DenyLocalLogin</c>, so it can't be detected server side and is matched by name instead.
        /// </remarks>
        public const string UmbracoIdProvider = "Umbraco.UmbracoId";

        /// <summary>
        /// Resolves the identity provider for the editor.
        /// </summary>
        /// <param name="configuredProvider">The configured provider, if any.</param>
        /// <param name="registeredProviders">The registered back office external login authentication schemes.</param>
        /// <param name="denyLocalLogin">Whether a registered provider denies local login.</param>
        /// <param name="configuredProviderNotFound">
        /// <see langword="true"/> when <paramref name="configuredProvider"/> is set but isn't a registered provider.
        /// </param>
        /// <returns>
        /// The external provider scheme to send as <c>identity_provider</c>, or <see langword="null"/> to use the
        /// default local back office login.
        /// </returns>
        public static string? Resolve(
            string? configuredProvider,
            IReadOnlyCollection<string> registeredProviders,
            bool denyLocalLogin,
            out bool configuredProviderNotFound)
        {
            configuredProviderNotFound = false;

            if (!string.IsNullOrWhiteSpace(configuredProvider))
            {
                string trimmed = configuredProvider.Trim();
                if (string.Equals(trimmed, LocalProvider, StringComparison.Ordinal))
                {
                    return null;
                }

                // Authentication scheme names are case-sensitive; an unknown scheme would make the challenge throw.
                if (registeredProviders.Contains(trimmed, StringComparer.Ordinal))
                {
                    return trimmed;
                }

                configuredProviderNotFound = true;
                return null;
            }

            if (registeredProviders.Count == 1)
            {
                string onlyProvider = registeredProviders.First();
                if (denyLocalLogin || string.Equals(onlyProvider, UmbracoIdProvider, StringComparison.Ordinal))
                {
                    return onlyProvider;
                }
            }

            return null;
        }
    }
}
