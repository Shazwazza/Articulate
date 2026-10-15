#nullable enable

using Articulate.Services;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Articulate.Tests.Services
{
    [TestFixture]
    public class GiscusThemeResolutionTests
    {
        [Test]
        public void ResolveGiscusDataTheme_UsesPreferredColorScheme_WhenThemeAssetIsUnavailable()
        {
            var repository = new StubThemeRepository(null);

            string result = repository.ResolveGiscusDataTheme(
                "Custom",
                new DefaultHttpContext().Request,
                string.Empty);

            Assert.That(result, Is.EqualTo("preferred_color_scheme"));
        }

        [Test]
        public void ResolveGiscusDataTheme_UsesExplicitThemeBeforeAsset()
        {
            var repository = new StubThemeRepository("https://example.test/giscus.css");

            string result = repository.ResolveGiscusDataTheme(
                "Custom",
                new DefaultHttpContext().Request,
                "dark");

            Assert.That(result, Is.EqualTo("dark"));
        }

        private sealed class StubThemeRepository(string? assetUrl) : IArticulateThemeRepository
        {
            public string? GetThemeAssetUrl(string themeName, HttpRequest request) => assetUrl;

            public Task<IEnumerable<string>> GetDefaultThemesAsync() => Task.FromResult<IEnumerable<string>>([]);

            public Task<IEnumerable<string>?> GetAllThemesAsync() => Task.FromResult<IEnumerable<string>?>([]);

            Task IArticulateThemeRepository.CopyThemeAsync(string themeName, string newThemeName) => Task.CompletedTask;
        }
    }
}
