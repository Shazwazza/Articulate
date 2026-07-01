#nullable enable
using Articulate.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Articulate.Controllers
{
    /// <summary>
    /// Proxies the per-theme <c>giscus.css</c> with the CORS headers giscus.app's
    /// iframe needs. giscus hard-codes <c>crossorigin="anonymous"</c> on the
    /// <c>&lt;link rel="stylesheet"&gt;</c> it injects; without
    /// <c>Access-Control-Allow-Origin</c> on the response, the stylesheet is
    /// rejected and the widget hangs at "Loading comments…" waiting for a load
    /// event that never fires.
    /// </summary>
    /// <remarks>
    /// Single code path: the controller fetches
    /// <c>/App_Plugins/Articulate/Themes/{theme}/assets/giscus.css</c> from the
    /// host via <c>HttpClient</c>. Umbraco's static-web-assets middleware serves that
    /// URL for every theme source — built-in (Articulate.Web), copied/forked
    /// (filesystem at <c>wwwroot/App_Plugins/.../</c>), and package/RCL themes
    /// (package install dir). One path, every source.
    /// <para>
    /// If the upstream returns 404 (theme has no <c>giscus.css</c>), the controller
    /// returns a no-op CSS body so giscus initializes with its built-in palette
    /// instead of hanging on a load failure.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("articulate/giscus-theme")]
    public class GiscusThemeController(
        ILogger<GiscusThemeController> logger,
        IOptions<ArticulateCommentsOptions> commentsOptions,
        IHttpClientFactory httpClientFactory) : Controller
    {
        private const string GiscusFileName = "giscus.css";
        private const string StaticAssetPathSegment = "/App_Plugins/Articulate/Themes/";

        [HttpGet("{theme}")]
        public async Task<IActionResult> Get(string theme)
        {
            if (!IsValidThemeName(theme))
            {
                return BadRequest();
            }

            string assetUrl = BuildAssetUrl(theme);

            // Factory-provided HttpClient wraps the rotated SocketsHttpHandler registered
            // in ArticulateComposer. The handler is reused across requests (default 2-min
            // rotation), so we don't allocate a new HttpClient on every request.
            using HttpClient http = httpClientFactory.CreateClient(ArticulateConstants.Comments.GiscusTheme.HttpClientName);
            using HttpResponseMessage response = await http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            // CORS: reflect the request Origin against the configured allowlist (default
            // ["https://giscus.app"]). The response also carries Vary: Origin when the
            // origin is reflected, so shared caches don't poison Allow-Origin between
            // different callers. Falls back to "*" for same-origin / no-Origin callers.
            CorsHeaderDecision cors = GiscusCommentsOptions.ResolveCorsHeaders(
                Request.Headers["Origin"].FirstOrDefault(),
                commentsOptions.Value.Giscus.AllowedCorsOrigins);
            if (cors.AllowOrigin is not null)
            {
                Response.Headers["Access-Control-Allow-Origin"] = cors.AllowOrigin;
            }
            if (cors.Vary)
            {
                Response.Headers["Vary"] = "Origin";
            }
            Response.Headers["Cache-Control"] = "public, max-age=3600";

            if (response.IsSuccessStatusCode)
            {
                byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return File(bytes, "text/css; charset=utf-8");
            }

            // Theme has no giscus.css (custom theme that ships none). Return 200 with a
            // no-op body so giscus initializes with its built-in palette instead of
            // hanging on a never-resolving <link>.
            logger.LogDebug("No giscus.css found at '{Url}'; returning empty body.", assetUrl);
            return Content($"/* no giscus.css for theme '{theme}' */\n", "text/css; charset=utf-8");
        }

        /// <summary>
        /// Builds the host-relative URL of the per-theme <c>giscus.css</c> under static-web-assets.
        /// </summary>
        private string BuildAssetUrl(string theme)
        {
            string baseUri = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            return baseUri.TrimEnd('/') + StaticAssetPathSegment + Uri.EscapeDataString(theme) + "/assets/giscus.css";
        }

        /// <summary>
        /// Rejects path-traversal and any character that wouldn't appear in a normal
        /// theme key. Matches the normalization in
        /// <see cref="Articulate.Services.ArticulateThemeRepository.TryNormalizeThemeKey"/>.
        /// </summary>
        private static bool IsValidThemeName(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
            {
                return false;
            }
            if (theme.Contains('/') || theme.Contains('\\'))
            {
                return false;
            }
            if (theme.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }
            return true;
        }
    }
}