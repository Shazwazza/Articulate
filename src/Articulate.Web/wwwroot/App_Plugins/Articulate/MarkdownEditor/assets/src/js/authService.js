/* Simple OAuth2/OpenID Connect client for the standalone Markdown editor.
   Flow summary:
   - redirectToLogin(): starts an authorization code + PKCE flow against Umbraco's
     back-office OpenIddict endpoints (using config.authUrl).
   - handleLoginCallback(): validates the state/code, then exchanges the
     authorization code for an access token via config.tokenUrl.
   - getAccessToken()/hasValidAccessToken(): manage the cached access token
     (stored only in memory) and perform lightweight client-side expiry checks.
   - logout(): best-effort revokes the cached access token (config.revokeUrl),
     then calls the end-session endpoint (config.authEndUrl) and finally clears
     the in-memory token before redirecting the browser.

*/

import { config } from './config.js';

let accessToken = null;

function resolveSafeSameOriginUrl(value, fallback = `${window.location.origin}/`) {
  if (typeof value !== 'string' || value.trim() === '') {
    return fallback;
  }

  try {
    const parsed = new URL(value, window.location.origin);
    if (parsed.origin !== window.location.origin) {
      return fallback;
    }

    return parsed.toString();
  } catch (error) {
    return fallback;
  }
}

async function generateCodeChallenge(codeVerifier) {
  const encoder = new TextEncoder();
  const data = encoder.encode(codeVerifier);
  const digest = await window.crypto.subtle.digest('SHA-256', data);
  return base64urlencode(digest);
}

function generateRandomString(byteLength) {
  const array = new Uint8Array(byteLength);
  window.crypto.getRandomValues(array);
  return Array.from(array, dec => dec.toString(16).padStart(2, '0')).join('');
}

function base64urlencode(buffer) {
  return btoa(String.fromCharCode.apply(null, new Uint8Array(buffer)))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

function base64UrlDecode(value) {
  if (typeof value !== 'string') {
    return null;
  }

  let normalized = value.replace(/-/g, '+').replace(/_/g, '/');
  while (normalized.length % 4 !== 0) {
    normalized += '=';
  }

  try {
    return window.atob(normalized);
  } catch (error) {
    console.warn('[authService] Failed to decode JWT payload', error);
    return null;
  }
}

function parseJwtPayload(token) {
  if (typeof token !== 'string') {
    return null;
  }

  const parts = token.split('.');
  if (parts.length < 2) {
    return null;
  }

  const decoded = base64UrlDecode(parts[1]);
  if (!decoded) {
    return null;
  }

  try {
    return JSON.parse(decoded);
  } catch (error) {
    console.warn('[authService] Failed to parse JWT payload', error);
    return null;
  }
}

// Token and session management
function getAccessToken() {
  return accessToken;
}

function setAccessToken(token) {
  if (token) {
    accessToken = token;
  }
}

function clearAccessToken() {
  accessToken = null;
}

async function revokeAccessToken() {
  const token = getAccessToken();
  if (!token || !config.revokeUrl) {
    return;
  }

  try {
    const params = new URLSearchParams();
    params.set('token', token);
    params.set('token_type_hint', 'access_token');
    if (config.oauth?.clientId) {
      params.set('client_id', config.oauth.clientId);
    }

    const response = await fetch(config.revokeUrl, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'Accept': 'application/json'
      },
      body: params.toString()
    });

    if (!response.ok) {
      console.warn('[authService] Token revocation returned non-success status:', response.status);
    }
  } catch (error) {
    console.warn('[authService] Failed to revoke access token.', error);
  }
}

// Main authentication functions
async function redirectToLogin() {
  const state = generateRandomString(32);
  const codeVerifier = generateRandomString(64);

  sessionStorage.setItem(config.storageKeys.oauthState, state);
  sessionStorage.setItem(config.storageKeys.codeVerifier, codeVerifier);


  const codeChallenge = await generateCodeChallenge(codeVerifier);

  const params = new URLSearchParams({
    client_id: config.oauth.clientId,
    redirect_uri: config.oauth.redirectUri,
    response_type: config.oauth.responseType,
    scope: config.oauth.scope,
    state: state,
    code_challenge: codeChallenge,
    code_challenge_method: config.oauth.codeChallengeMethod,
  });

  window.location.href = `${config.authUrl}?${params.toString()}`;
}

async function handleLoginCallback() {
  const params = new URLSearchParams(window.location.search);
  const code = params.get('code');
  const state = params.get('state');
  const storedState = sessionStorage.getItem(config.storageKeys.oauthState);
  const codeVerifier = sessionStorage.getItem(config.storageKeys.codeVerifier);

  window.history.replaceState({}, document.title, window.location.pathname);
  sessionStorage.removeItem(config.storageKeys.oauthState);
  sessionStorage.removeItem(config.storageKeys.codeVerifier);


  if (!code || !state || state !== storedState) {
    throw new Error('Invalid state or code from authentication server.');
  }

  if (!codeVerifier) {
    throw new Error('Code verifier not found in session storage.');
  }

  const tokenResponse = await fetch(config.tokenUrl, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
    body: new URLSearchParams({
      grant_type: 'authorization_code',
      code: code,
      redirect_uri: config.oauth.redirectUri,
      client_id: config.oauth.clientId,
      code_verifier: codeVerifier,
    }),
  });

  if (!tokenResponse.ok) {
    throw new Error('Failed to exchange authorization code for access token.');
  }

  const tokenData = await tokenResponse.json();

  setAccessToken(tokenData.access_token);

  if (!hasValidAccessToken()) {
    throw new Error('Access token is invalid or expired.');
  }
}

function hasValidAccessToken() {
  const token = getAccessToken();
  if (!token) {
    return false;
  }

  const payload = parseJwtPayload(token);
  if (!payload) {
    return true;
  }

  if (Object.prototype.hasOwnProperty.call(payload, 'exp')) {
    const exp = Number(payload.exp);
    if (!Number.isNaN(exp)) {
      const now = Math.floor(Date.now() / 1000);
      const skewAllowance = 60; // seconds
      if (exp <= now + skewAllowance) {
        clearAccessToken();
        console.info('[authService] Detected expired access token; clearing cached value.');
        return false;
      }
    }
  }

  return true;
}

async function logout() {
  const fallbackRedirectUrl = `${window.location.origin}/`;
  let redirectUrl = resolveSafeSameOriginUrl(config.postLogoutRedirectUrl, fallbackRedirectUrl);

  try {
    await revokeAccessToken();

    const logoutUrl = new URL(config.authEndUrl, window.location.origin);
    logoutUrl.searchParams.set('post_logout_redirect_uri', redirectUrl);

    const response = await fetch(logoutUrl.toString(), {
      method: 'GET',
      credentials: 'include',
      headers: {
        'Accept': 'application/json'
      }
    });

    if (response.ok) {
      const responseText = await response.text();

      if (responseText) {
        try {
          const payload = JSON.parse(responseText);
          if (payload && typeof payload.signOutRedirectUrl === 'string' && payload.signOutRedirectUrl.trim() !== '') {
            redirectUrl = resolveSafeSameOriginUrl(payload.signOutRedirectUrl, redirectUrl);
          }
        } catch (parseError) {
          console.warn('[authService] Unexpected sign-out response payload.', parseError);
        }
      }
    } else {
      console.warn('[authService] Sign-out request returned non-success status:', response.status);
    }
  } catch (error) {
    console.warn('[authService] Failed to sign out from Umbraco.', error);
  } finally {
    clearAccessToken();
    window.location.assign(redirectUrl);
  }
}

export const authService = {
  redirectToLogin,
  handleLoginCallback,
  getAccessToken,
  hasValidAccessToken,
  logout
};
