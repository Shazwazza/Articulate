#nullable enable
using Paths = Articulate.ArticulateConstants.Paths;

namespace Articulate.Services
{
    internal sealed class DefaultArticulateViewLocationProvider : IArticulateViewLocationProvider
    {
        public IEnumerable<string> GetLocations(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                return [];
            }

            var locations = new List<string>
            {
                // User themes (preferred structure - with Views subfolder)
                BuildPath(Paths.UserThemesRoot, themeName, Paths.Views, Paths.ViewPlaceholder),
                BuildPath(Paths.UserThemesRoot, themeName, Paths.Views, Paths.Partials, Paths.ViewPlaceholder),

                // User themes (legacy flat structure kept for compatibility)
                BuildPath(Paths.UserThemesRoot, themeName, Paths.ViewPlaceholder),
                BuildPath(Paths.UserThemesRoot, themeName, Paths.Partials, Paths.ViewPlaceholder),

                // Built-in themes (after all user theme paths)
                BuildPath(Paths.ArticulateRoot, Paths.Themes, themeName, Paths.Views, Paths.ViewPlaceholder),
                BuildPath(Paths.ArticulateRoot, Paths.Themes, themeName, Paths.Views, Paths.Partials, Paths.ViewPlaceholder),

                // MarkdownEditor
                BuildPath(Paths.ArticulateRoot, Paths.MarkdownEditor, Paths.Views, Paths.ViewPlaceholder)
            };

            return locations.Distinct();
        }

        /// <summary>
        ///     Combines path segments and forces Forward Slashes for Razor View Engine compatibility.
        ///     Ensure the result starts with "/" to denote application root relative.
        /// </summary>
        private static string BuildPath(params string[] parts)
        {
            var combined = Path.Combine(parts);

            var webPath = combined.Replace('\\', '/');

            return webPath.StartsWith('/') ? webPath : "/" + webPath;
        }
    }
}
