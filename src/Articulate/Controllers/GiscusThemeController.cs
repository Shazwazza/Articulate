#nullable enable
using System.Net.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
        ILogger<GiscusThemeController> logger) : Controller
    {
        private const string GiscusFileName = "giscus.css";
        private const string StaticAssetPathSegment = "/App_Plugins/Articulate/Themes/";

        // Loopback self-call handler. Trusts the dev cert when the upstream URL
        // is loopback (always the case for our self-call); validates normally
        // for any other host. Static so the handler is reused across requests
        // and not disposed by per-call HttpClient instances.
        private static readonly HttpClientHandler LoopbackHandler = new()
        {
            ServerCertificateCustomValidationCallback = static (request, _, _, sslErrors) =>
                request.RequestUri?.IsLoopback == true || sslErrors == SslPolicyErrors.None,
        };

        [HttpGet("{theme}")]
        public async Task<IActionResult> Get(string theme)
        {
            if (!IsValidThemeName(theme))
            {
                return BadRequest();
            }

            string assetUrl = BuildAssetUrl(theme);

            using HttpClient http = new(LoopbackHandler, disposeHandler: false);
            using HttpResponseMessage response = await http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            Response.Headers["Access-Control-Allow-Origin"] = "*";
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