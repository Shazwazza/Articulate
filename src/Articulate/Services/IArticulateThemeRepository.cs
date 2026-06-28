#nullable enable
using Microsoft.AspNetCore.Http;

namespace Articulate.Services
{
    /// <summary>
    /// Repository interface for Articulate themes.
    /// </summary>
    public interface IArticulateThemeRepository
    {
        /// <summary>
        /// Gets the list of default Articulate themes.
        /// </summary>
        /// <returns>A collection of theme names.</returns>
        public Task<IEnumerable<string>> GetDefaultThemesAsync();

        /// <summary>
        /// Gets all available themes (default and user-defined).
        /// </summary>
        /// <returns>A collection of theme names, or null if none found.</returns>
        public Task<IEnumerable<string>?> GetAllThemesAsync();

        /// <summary>
        /// Resolves the request-absolute URL of a per-theme asset (e.g. <c>giscus.css</c>).
        /// </summary>
        /// <remarks>
        /// Returns the Articulate <c>GiscusThemeController</c> endpoint, which proxies
        /// the static-web-assets path with the CORS header giscus's cross-origin iframe needs.
        /// </remarks>
        /// <param name="themeName">Theme key (e.g. <c>"Material"</c>).</param>
        /// <param name="assetRelativePath">Path under <c>assets/</c> (currently always <c>giscus.css</c>).</param>
        /// <param name="request">The live request (LB-correct base URL is resolved from its scheme/host/path base).</param>
        /// <returns>The absolute asset URL, or <c>null</c> when <paramref name="themeName"/> is empty/whitespace.</returns>
        public string? GetThemeAssetUrl(string themeName, string assetRelativePath, HttpRequest request);

        /// <summary>
        /// Copies an existing embedded theme to the user themes directory.
        /// </summary>
        /// <param name="themeName">The name of the source theme.</param>
        /// <param name="newThemeName">The name for the copied theme.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal Task CopyThemeAsync(string themeName, string newThemeName);
    }
}
