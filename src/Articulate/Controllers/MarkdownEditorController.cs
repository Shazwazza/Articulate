#nullable enable
using Articulate.Attributes;
using Articulate.Controllers.Api;
using Articulate.Options;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;

namespace Articulate.Controllers
{
    /// <summary>
    ///     Controller for the Articulate Markdown editor.
    /// </summary>
    [ArticulateDynamicRoute]
    public class MarkdownEditorController(
        ILogger<MarkdownEditorController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IApiDescriptionGroupCollectionProvider apiDescriptionProvider,
        IOptions<ArticulateOpenIdClientOptions> artClientOptions,
        IOptions<WebRoutingSettings> webRoutingSettings)
        : RenderController(logger, compositeViewEngine, umbracoContextAccessor)
    {
        /// <summary>
        ///     Renders the view for creating a new post using the Markdown editor.
        /// </summary>
        /// <returns>The action result yielding the editor view.</returns>
        [HttpGet]
        public IActionResult NewPost()
        {
            if (CurrentPage is null)
            {
                logger.LogWarning("MarkdownEditorController.NewPost: CurrentPage is null, returning 404");
                return NotFound();
            }

            IReadOnlyDictionary<string, string>? managementApiUrls = apiDescriptionProvider.ManagementApiUrlMap([
                ArticulateConstants.ManagementApi.MarkdownEditor
            ]);

            var key = GetKey<MarkdownEditorApiController>(nameof(MarkdownEditorApiController.CreatePost));
            string? editorUrl = null;

            if (managementApiUrls?.TryGetValue(key, out var urlFromMap) == true)
            {
                editorUrl = urlFromMap;
            }

            if (string.IsNullOrWhiteSpace(editorUrl))
            {
                throw new InvalidOperationException(
                    $"Could not find the Management API URL for '{key}'. " +
                    "Check if the Articulate API routes are registered correctly at startup.");
            }

            Uri baseUri = GetAndValidateBaseUrl();
            editorUrl = new Uri(baseUri, editorUrl).ToString();

            if (!Uri.TryCreate(editorUrl, UriKind.Absolute, out Uri? editorAbsoluteUri))
            {
                throw new InvalidOperationException($"The editor URL '{editorUrl}' is not a valid absolute URI.");
            }

            if (CurrentPage.Id <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid Articulate node id '{CurrentPage.Id}' for Markdown editor initialization.");
            }

            ArticulateOpenIdClientOptions openIdClientOptions = artClientOptions.Value;
            OAuthUrls oauthUrls = BuildOAuthUrls(editorAbsoluteUri, openIdClientOptions);

            var vm = new MarkdownEditorInitModel
            {
                ArticulateBlogNode = CurrentPage.Id,
                EditorPostUrl = editorUrl,
                BackOfficeClientId = openIdClientOptions.ClientId ?? string.Empty,
                PostLogoutRedirectUrl = GetDefaultPostLogoutRedirectUrl(baseUri, openIdClientOptions),
                AuthorizeUrl = oauthUrls.AuthorizeUrl,
                CurrentUserUrl = oauthUrls.CurrentUserUrl,
                EndSessionUrl = oauthUrls.EndSessionUrl,
                TokenUrl = oauthUrls.TokenUrl,
                RevocationUrl = oauthUrls.RevocationUrl,
                LoginLogoUrl = oauthUrls.LoginLogoUrl
            };

            SetSecurityHeaders();
            return View("MarkdownEditor", vm);

            static string GetKey<T>(string actionName) => $"{typeof(T).Name}.{actionName}";
        }

        private Uri GetAndValidateBaseUrl()
        {
            var isConfiguredUrl = !string.IsNullOrWhiteSpace(webRoutingSettings.Value.UmbracoApplicationUrl);
            var baseUrl = isConfiguredUrl
                ? webRoutingSettings.Value.UmbracoApplicationUrl
                : UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase);

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
            {
                logger.LogError(
                    "Invalid base URL configuration. Source: {Source}, Value: {BaseUrl}",
                    isConfiguredUrl ? "UmbracoApplicationUrl" : "Request-derived",
                    baseUrl);
                throw new InvalidOperationException(
                    $"The Umbraco application base URL '{baseUrl}' is not a valid absolute URI. " +
                    "Configure 'Umbraco:CMS:WebRouting:UmbracoApplicationUrl' in appsettings.json.");
            }

            if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            {
                logger.LogError(
                    "Invalid base URL scheme. Expected http/https, got {Scheme} for URL: {BaseUrl}",
                    baseUri.Scheme,
                    baseUrl);
                throw new InvalidOperationException(
                    $"The base URL must use http or https scheme, got '{baseUri.Scheme}': {baseUrl}");
            }

            if (!isConfiguredUrl)
            {
                logger.LogWarning(
                    "UmbracoApplicationUrl not configured, using Request-derived base URL. " +
                    "Host: {Host}, Scheme: {Scheme}. Configure 'Umbraco:CMS:WebRouting:UmbracoApplicationUrl' " +
                    "in appsettings.json to avoid relying on request headers.",
                    Request.Host,
                    Request.Scheme);
            }

            return baseUri;
        }

        private OAuthUrls BuildOAuthUrls(Uri editorAbsoluteUri, ArticulateOpenIdClientOptions options)
        {
            var umbracoPath = GetUmbracoPathFromManagementApiUrl(editorAbsoluteUri);

            // These are the built-in Umbraco/OpenIddict endpoints used by the standalone editor.
            // RedirectUris and PostLogoutRedirectUris are configured on the OpenIddict client registration,
            // not by changing these endpoint URLs.
            var defaultAuthorizeUrl = BuildAbsoluteUrl(
                editorAbsoluteUri,
                $"{umbracoPath}/management/api/v1/security/back-office/authorize");
            var defaultTokenUrl = BuildAbsoluteUrl(
                editorAbsoluteUri,
                $"{umbracoPath}/management/api/v1/security/back-office/token");
            var defaultEndSessionUrl = BuildAbsoluteUrl(
                editorAbsoluteUri,
                $"{umbracoPath}/management/api/v1/security/back-office/signout");
            var defaultRevocationUrl = BuildAbsoluteUrl(
                editorAbsoluteUri,
                $"{umbracoPath}/management/api/v1/security/back-office/revoke");
            var defaultCurrentUserUrl =
                BuildAbsoluteUrl(editorAbsoluteUri, $"{umbracoPath}/management/api/v1/user/current");
            var defaultLoginLogoUrl = BuildAbsoluteUrl(
                editorAbsoluteUri,
                $"{umbracoPath}/management/api/v1/security/back-office/graphics/login-logo-alternative");

            static string UseConfiguredOrDefault(string? value, string fallback) =>
                string.IsNullOrWhiteSpace(value) ? fallback : value;

            return new OAuthUrls(
                UseConfiguredOrDefault(options.AuthorizeUrl, defaultAuthorizeUrl),
                UseConfiguredOrDefault(options.TokenUrl, defaultTokenUrl),
                UseConfiguredOrDefault(options.EndSessionUrl, defaultEndSessionUrl),
                UseConfiguredOrDefault(options.RevocationUrl, defaultRevocationUrl),
                UseConfiguredOrDefault(options.CurrentUserUrl, defaultCurrentUserUrl),
                UseConfiguredOrDefault(options.LoginLogoUrl, defaultLoginLogoUrl));

            static string BuildAbsoluteUrl(Uri baseUri, string path)
            {
                if (!path.StartsWith('/'))
                {
                    path = "/" + path;
                }

                var builder = new UriBuilder(baseUri) { Path = path, Query = string.Empty, Fragment = string.Empty };
                return builder.Uri.ToString();
            }

            static string GetUmbracoPathFromManagementApiUrl(Uri managementApiUri)
            {
                const string defaultUmbracoPath = Constants.System.DefaultUmbracoPath;
                var defaultNormalized = Normalize(defaultUmbracoPath);

                var path = managementApiUri.AbsolutePath;

                const string marker = "/management/api/";
                var markerIndex = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex <= 0)
                {
                    return defaultNormalized;
                }

                var prefix = path[..markerIndex];
                prefix = string.IsNullOrWhiteSpace(prefix) ? defaultUmbracoPath : prefix;

                prefix = Normalize(prefix);
                return string.IsNullOrWhiteSpace(prefix) ? defaultNormalized : prefix;

                static string Normalize(string value)
                {
                    var normalized = value;
                    normalized = normalized.TrimStart('~');
                    normalized = normalized.EnsureStartsWith('/');
                    normalized = normalized.TrimEnd('/');
                    return normalized;
                }
            }
        }

        private static string GetDefaultPostLogoutRedirectUrl(Uri baseUri, ArticulateOpenIdClientOptions options)
        {
            foreach (var candidate in options.PostLogoutRedirectUris)
            {
                if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? redirectUri))
                {
                    return redirectUri.ToString();
                }
            }

            return new UriBuilder(baseUri) { Path = "/", Query = string.Empty, Fragment = string.Empty }.Uri.ToString();
        }

        private void SetSecurityHeaders()
        {
            Response.Headers["Permissions-Policy"] = "camera=(self)";
            Response.Headers["Content-Security-Policy"] = string.Join(
                ";",
                new[]
                {
                    "default-src 'self'", "script-src 'self'", "style-src 'self'", "img-src 'self' data: blob:",
                    "font-src 'self'", "connect-src 'self'", "frame-ancestors 'self'", "base-uri 'self'",
                    "object-src 'none'"
                });
        }

        private record OAuthUrls(
            string AuthorizeUrl,
            string TokenUrl,
            string EndSessionUrl,
            string RevocationUrl,
            string CurrentUserUrl,
            string LoginLogoUrl);
    }
}
