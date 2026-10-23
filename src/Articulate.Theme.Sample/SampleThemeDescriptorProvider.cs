using Articulate.Services;

namespace Articulate.Theme.Sample
{
    public sealed class SampleThemeDescriptorProvider : IArticulateThemeDescriptorProvider
    {
        public IEnumerable<string> GetThemeKeys()
        {
            yield return SampleTheme.ThemeKey;
        }
    }
}
