using Umbraco.Core.ObjectResolution;

namespace Articulate
{
    /// <summary>
    /// Resolves the <see cref="IThemeManager"/> implementation.
    /// </summary>
    public class ThemeManagerResolver : SingleObjectResolverBase<ThemeManagerResolver, IThemeManager>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeManagerResolver"/> class with an <see cref="IThemeManager"/> implementation.
        /// </summary>
        /// <param name="helper">The <see cref="IThemeManager"/> implementation.</param>
        internal ThemeManagerResolver(IThemeManager manager)
            : base(manager)
        {
        }

        /// <summary>
        /// Can be used at runtime to set a custom IThemeManager at app startup
        /// </summary>
        /// <param name="serverRegistrar"></param>
        public void SetThemeManager(IThemeManager manager)
        {
            Value = manager;
        }

        /// <summary>
        /// Gets the <see cref="IThemeManager"/> implementation.
        /// </summary>
        public IThemeManager ThemeManager
        {
            get { return Value; }
        }
    }
}
