#nullable enable
using Articulate.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Articulate.Controllers
{
    /// <summary>
    /// Serves the per-theme <c>giscus.css</c> with the CORS headers giscus.app's
    /// iframe needs. giscus hard-codes <c>crossorigin="anonymous"</c> on the
    /// <c>&lt;link rel="stylesheet"&gt;</c> it injects; without
    /// <c>Access-Control-Allow-Origin</c> on the response, the stylesheet is
    /// rejected and the widget hangs at "Loading comments…" waiting for a load
    /// event that never fires.
    /// </summary>
    /// <remarks>
    /// Reads <c>App_Plugins/Articulate/Themes/{theme}/assets/giscus.css</c> directly
    /// off disk via <see cref="IWebHostEnvironment.WebRootPath"/>. The same path
    /// is also served by Umbraco's static-web-assets middleware at
    /// <c>/App_Plugins/Articulate/Themes/{theme}/assets/giscus.css</c>; this
    /// controller is the cross-origin version for the giscus iframe.
    /// <para>
    /// If the file is missing (custom theme that ships none), the controller
    /// returns a no-op CSS body so giscus initializes with its built-in palette
    /// instead of hanging on a load failure.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("articulate/giscus-theme")]
    public class GiscusThemeController(
        ILogger<GiscusThemeController> logger,
        IOptions<ArticulateCommentsOptions> commentsOptions,
        IWebHostEnvironment webHostEnvironment) : Controller
    {
        private const string GiscusFileName = "giscus.css";

        [HttpGet("{theme}")]
        public async Task<IActionResult> Get(string theme)
        {
            if (!IsValidThemeName(theme))
            {
                return BadRequest();
            }

            var filePath = Path.Combine(
                webHostEnvironment.WebRootPath,
                "App_Plugins",
                "Articulate",
                "Themes",
                Uri.EscapeDataString(theme),
                "assets",
                GiscusFileName);

            ApplyCorsHeaders();
            Response.Headers["Cache-Control"] = "public, max-age=3600";

            if (!System.IO.File.Exists(filePath))
            {
                // Theme has no giscus.css (custom theme that ships none). Return 200 with a
                // no-op body so giscus initializes with its built-in palette instead of
                // hanging on a never-resolving <link>.
                logger.LogDebug("No giscus.css at '{Path}'; returning empty body.", filePath);
                return Content($"/* no giscus.css for theme '{theme}' */\n", "text/css; charset=utf-8");
            }

            byte[] bytes = await System.IO.File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
            return File(bytes, "text/css; charset=utf-8");
        }

        // Reflect the request Origin against the configured allowlist (default
        // ["https://giscus.app"]). Vary: Origin is set on reflection so shared
        // caches don't poison Allow-Origin between different callers. Falls back
        // to "*" for same-origin / no-Origin callers.
        private void ApplyCorsHeaders()
        {
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