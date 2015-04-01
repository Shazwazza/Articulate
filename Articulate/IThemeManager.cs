using System.Collections.Generic;

namespace Articulate
{
    public interface IThemeManager
    {
        /// <summary>
        /// Adds or updates a theme for a package
        /// </summary>
        /// <param name="package">The package name</param>
        /// <param name="theme">The theme name</param>
        void AddOrUpdatePackageTheme(string package, string theme);

        /// <summary>
        /// Removes a theme for a package
        /// </summary>
        /// <param name="packageName">the theme name</param>
        /// <returns></returns>
        bool RemovePackageTheme(string theme);
    }
}
