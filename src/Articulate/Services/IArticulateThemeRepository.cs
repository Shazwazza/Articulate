#nullable enable
namespace Articulate.Services
{
    /// <summary>
    ///     Repository interface for Articulate themes.
    /// </summary>
    public interface IArticulateThemeRepository
    {
        /// <summary>
        ///     Gets the list of default Articulate themes.
        /// </summary>
        /// <returns>A collection of theme names.</returns>
        public Task<IEnumerable<string>> GetDefaultThemesAsync();

        /// <summary>
        ///     Gets all available themes (default and user-defined).
        /// </summary>
        /// <returns>A collection of theme names, or null if none found.</returns>
        public Task<IEnumerable<string>?> GetAllThemesAsync();

        /// <summary>
        ///     Copies an existing embedded theme to the user themes directory.
        /// </summary>
        /// <param name="themeName">The name of the source theme.</param>
        /// <param name="newThemeName">The name for the copied theme.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal Task CopyThemeAsync(string themeName, string newThemeName);
    }
}
