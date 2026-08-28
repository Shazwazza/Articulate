function ensureTrailingSlash(value) {
    if (typeof value !== "string" || value.length === 0) {
        return value;
    }

    try {
        const parsed = new URL(value, window.location.origin);
        if (!parsed.pathname.endsWith("/")) {
            parsed.pathname = `${parsed.pathname}/`;
        }
        return parsed.toString();
    } catch (error) {
        const hashIndex = value.indexOf("#");
        const base = hashIndex === -1 ? value : value.slice(0, hashIndex);
        const hash = hashIndex === -1 ? "" : value.slice(hashIndex);

        if (base.endsWith("/")) {
            return `${base}${hash}`;
        }

        return `${base}/${hash}`;
    }
}

// Holds all configuration, initialized dynamically from the main view.
const config = {
    // Dynamic URLs from the view's dataset
    // authUrl: OAuth/OIDC authorization endpoint used to start the login flow.
    authUrl: '',
    // authEndUrl: end-session (sign-out) endpoint used during logout.
    authEndUrl: '',
    // tokenUrl: token endpoint used to exchange the authorization code for an access token.
    tokenUrl: '',
    // currentUserUrl: Management API endpoint used to resolve the current back-office user.
    currentUserUrl: '',
    // editorPostUrl: Management API endpoint used to create the blog post.
    editorPostUrl: '',
    // postLogoutRedirectUrl: explicit destination requested during end-session.
    postLogoutRedirectUrl: '/',
    articulateBlogNode: null,
    // Optional token revocation endpoint used by authService.logout() before sign-out.
    revokeUrl: '',

    // Static OAuth parameters
    oauth: {
        clientId: '',
        redirectUri: ensureTrailingSlash(window.location.href.split('?')[0]), // The current URL without query params
        scope: 'openid',
        responseType: 'code',
        codeChallengeMethod: 'S256',
    },

    // Keys for browser storage
    storageKeys: {
        codeVerifier: 'articulate_code_verifier',
        oauthState: 'articulate_oauth_state',
    }
};

/**
 * Initializes the configuration object with dynamic values from the DOM.
 * @param {DOMStringMap} dataset The dataset from the document body.
 */
function initConfig(dataset) {
    const {
        authUrl,
        authEndUrl,
        tokenUrl,
        currentUserUrl,
        editorPostUrl,
        postLogoutRedirectUrl,
        articulateBlogNode,
        oauthClientId,
        revocationUrl,
    } = dataset;

    if (!authUrl || !editorPostUrl || !currentUserUrl || !tokenUrl || !authEndUrl || !articulateBlogNode) {
        console.error("CRITICAL: One or more dataset values missing. The application cannot function.");
        throw new Error("Missing critical configuration from dataset.");
    }

    config.authUrl = authUrl;
    config.authEndUrl = authEndUrl;
    config.tokenUrl = tokenUrl;
    config.currentUserUrl = currentUserUrl;
    config.editorPostUrl = editorPostUrl;
    config.postLogoutRedirectUrl = ensureTrailingSlash(
        typeof postLogoutRedirectUrl === "string" && postLogoutRedirectUrl.trim().length
            ? postLogoutRedirectUrl.trim()
            : "/");
    config.articulateBlogNode = articulateBlogNode;

    const trimmedClientId = typeof oauthClientId === "string" ? oauthClientId.trim() : "";
    if (!trimmedClientId) {
        console.error("CRITICAL: OAuth client configuration missing.");
        throw new Error("Missing OAuth client configuration.");
    }

    config.oauth.clientId = trimmedClientId;
    config.oauth.redirectUri = ensureTrailingSlash(config.oauth.redirectUri);

    if (typeof revocationUrl === "string") {
        config.revokeUrl = revocationUrl.trim();
    }
}

export { config, initConfig };

