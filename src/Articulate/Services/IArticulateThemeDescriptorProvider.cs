#nullable enable
namespace Articulate.Services
{
    /// <summary>
    ///     Provides canonical theme keys contributed by installed packages.
    /// </summary>
    public interface IArticulateThemeDescriptorProvider
    {
        /// <summary>
        ///     Gets canonical theme keys exposed by the current package.
        /// </summary>
        /// <remarks>
        ///     A theme key is the stable value saved by the theme picker and must match the package theme folder name
        ///     in <c>App_Plugins/Articulate/Themes/{ThemeKey}</c>. Built-in theme keys are reserved.
        /// </remarks>
        /// <returns>A collection of canonical theme keys.</returns>
        public IEnumerable<string> GetThemeKeys();
    }
}
