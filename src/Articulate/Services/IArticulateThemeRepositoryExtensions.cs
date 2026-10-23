#nullable enable
using Microsoft.AspNetCore.Http;

namespace Articulate.Services
{
    public static class IArticulateThemeRepositoryExtensions
    {
        /// <summary>
        /// Resolves the giscus <c>data-theme</c> using operator-first precedence:
        /// <list type="number">
        /// <item>An explicit, non-empty <paramref name="explicitTheme"/> (keyword or URL) applies first.</item>
        /// <item>Otherwise, use the active theme's asset URL when non-null.</item>
        /// <item>Otherwise, giscus uses its built-in palette.</item>
        /// </list>
        /// </summary>
        public static string ResolveGiscusDataTheme(
            this IArticulateThemeRepository repo,
            string themeName,
            HttpRequest request,
            string explicitTheme)
            => !string.IsNullOrEmpty(explicitTheme)
                ? explicitTheme
                : (repo.GetThemeAssetUrl(themeName, request) ?? "preferred_color_scheme");
    }
}
