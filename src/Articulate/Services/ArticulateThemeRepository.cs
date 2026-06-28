#nullable enable
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using static Articulate.ArticulateConstants;

namespace Articulate.Services
{
    /// <summary>
    /// Repository for retrieving and managing Articulate themes.
    /// </summary>
    public sealed class ArticulateThemeRepository(
        IWebHostEnvironment hostingEnvironment,
        ILogger<ArticulateThemeRepository> logger,
        AppCaches appCaches,
        IEnumerable<IArticulateThemeDescriptorProvider> themeDescriptorProviders)
        : IArticulateThemeRepository
    {
        private const string AllThemesCacheKey = "Articulate_AllThemes";
        private const string EmbeddedResourceRoot = "Articulate.Theme://";

        /// <inheritdoc/>
        public string? GetThemeAssetUrl(string themeName, string assetRelativePath, HttpRequest request)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(themeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(assetRelativePath);
            ArgumentNullException.ThrowIfNull(request);

            // Always point at the GiscusThemeController endpoint. The controller proxies
            // the static-web-assets URL with the Access-Control-Allow-Origin header
            // giscus.app's iframe requires. Umbraco's static-web-assets middleware serves
            // /App_Plugins/Articulate/Themes/{theme}/assets/{file} for every theme
            // source (built-in, copied, RCL), so a single URL covers them all.
            string baseUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase);
            return baseUri.TrimEnd('/') + $"/articulate/giscus-theme/{themeName}";
        }

        /// <inheritdoc/>
        async Task IArticulateThemeRepository.CopyThemeAsync(string themeName, string newThemeName)
        {
            if (DefaultThemes.AllThemeNames.Any(theme => string.Equals(theme, newThemeName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    $@"The theme name '{newThemeName}' is reserved for a built-in theme.",
                    nameof(newThemeName));
            }

            var userThemesPath = Path.GetFullPath(
                Path.Combine(hostingEnvironment.ContentRootPath, Paths.UserThemesRoot));
            var themeRootDestination = Path.GetFullPath(
                Path.Combine(userThemesPath, newThemeName));
            var viewsDestination = Path.Combine(themeRootDestination, Paths.Views);

            if (!themeRootDestination.StartsWith(userThemesPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(@"Invalid theme name", nameof(newThemeName));
            }

            Assembly articulateAssembly = GetWebAssembly();

            // Check if theme already exists
            if (Directory.Exists(themeRootDestination))
            {
                throw new IOException($"A user theme with the name '{newThemeName}' already exists.");
            }

            // Separate resources by type
            var viewResources = GetThemeResourcesByType(articulateAssembly, themeName, "Views").ToList();
            var assetResources = GetThemeResourcesByType(articulateAssembly, themeName, "assets").ToList();

            if (viewResources.Count == 0 && assetResources.Count == 0)
            {
                throw new DirectoryNotFoundException(
                    $"The source theme '{themeName}' could not be found as an embedded resource.");
            }

            try
            {
                // Extract views to Views/ArticulateThemes/{newThemeName}/Views/
                _ = Directory.CreateDirectory(viewsDestination);
                await ExtractResourcesAsync(
                    articulateAssembly,
                    viewResources,
                    viewsDestination,
                    themeName,
                    "Views/");

                logger.LogInformation(
                    "Copied views for theme '{NewThemeName}' from '{SourceTheme}' to {ViewsPath}",
                    newThemeName,
                    themeName,
                    Path.Combine("Views", "ArticulateThemes", newThemeName, "Views"));

                // Extract Assets to wwwroot/App_Plugins/Articulate/Themes/{newThemeName}/assets/
                if (assetResources.Count > 0)
                {
                    var assetsDestination = Path.Combine(
                        hostingEnvironment.WebRootPath,
                        "App_Plugins",
                        "Articulate",
                        "Themes",
                        newThemeName,
                        "assets");

                    _ = Directory.CreateDirectory(assetsDestination);
                    await ExtractResourcesAsync(
                        articulateAssembly,
                        assetResources,
                        assetsDestination,
                        themeName,
                        "assets/");

                    logger.LogInformation(
                        "Copied assets for theme '{NewThemeName}' to {AssetsPath}",
                        newThemeName,
                        Path.Combine("wwwroot", "App_Plugins", "Articulate", "Themes", newThemeName, "assets"));
                }

                // Create helpful README
                await CreateThemeReadmeAsync(themeRootDestination, themeName, newThemeName);

                appCaches.RuntimeCache.ClearByKey(AllThemesCacheKey);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error copying embedded theme '{SourceTheme}' to '{DestinationTheme}'",
                    themeName,
                    newThemeName);
                throw;
            }
        }

        private static IEnumerable<string> GetThemeResourcesByType(
            Assembly assembly,
            string themeName,
            string resourceType)
        {
            // Manifest resource names can contain either '/' or '\\' depending on build/platform.
            // Normalize to '/' so prefix + relative path logic is consistent.
            var prefix = $"{EmbeddedResourceRoot}Themes/{themeName}/";
            var typePrefix = $"{resourceType}/";

            return assembly.GetManifestResourceNames()
                .Where(resource =>
                {
                    var normalizedResource = resource.Replace('\\', '/');

                    if (!normalizedResource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var relativePath = normalizedResource[prefix.Length..];
                    return relativePath.StartsWith(typePrefix, StringComparison.OrdinalIgnoreCase);
                });
        }

        private async Task ExtractResourcesAsync(
            Assembly assembly,
            List<string> resources,
            string destinationBase,
            string themeName,
            string prefixToStrip)
        {
            var prefixToRemove = $"{EmbeddedResourceRoot}Themes/{themeName}/";

            foreach (var resourceName in resources)
            {
                var normalizedResourceName = resourceName.Replace('\\', '/');
                if (!normalizedResourceName.StartsWith(prefixToRemove, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = normalizedResourceName[prefixToRemove.Length..];

                if (relativePath.StartsWith(prefixToStrip, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath[prefixToStrip.Length..];
                }

                var cleanPath = relativePath
                    .Replace('/', Path.DirectorySeparatorChar);

                var destinationFilePath = Path.Combine(destinationBase, cleanPath);
                await ExtractResourceToFileAsync(assembly, resourceName, destinationFilePath);
            }
        }

        private static async Task CreateThemeReadmeAsync(string themeRoot, string sourceTheme, string newTheme)
        {
            var readme = $$"""
                          # Articulate Theme: {{newTheme}}

                          Created by copying '{{sourceTheme}}' theme.

                          ## Folder Structure

                          **Views:** `Views/ArticulateThemes/{{newTheme}}/Views/`
                          Edit .cshtml files here to customize your theme layout.

                          **Assets:** `wwwroot/App_Plugins/Articulate/Themes/{{newTheme}}/assets/`
                          CSS, JavaScript, images, and other static files.

                          ## Quick Start

                          1. Edit `Views/Master.cshtml` - Main layout template
                          2. Edit `Views/Post.cshtml` - Individual blog post template
                          3. Customize CSS in `wwwroot/.../assets/css/`
                          4. Copied themes do not include a production build pipeline for assets, either set up your own build process, or ensure production builds link to src assets.

                          ## Updating a v5 theme to the modern layout

                          v5 themes used the `Master.cshtml` convention where every page view set
                          `Layout = "Master.cshtml"`. To update an existing v5 theme to the standard
                          ASP.NET Core Razor layout pattern:

                          1. Find every view in your theme that references the old layout:

                             ```bash
                             grep -rl 'Layout = "Master.cshtml"' path/to/your/theme/
                             ```

                          2. Rename `Views/Master.cshtml` to `Views/_Layout.cshtml`.
                          3. Add `Views/_ViewStart.cshtml` with:

                             ```cshtml
                             @{
                                 Layout = "_Layout.cshtml";
                             }
                             ```

                          4. Remove the `Layout = "Master.cshtml";` line from each page view found in
                             step 1.

                          Views that don't set their own `Layout` inherit `_Layout.cshtml` via
                          `_ViewStart.cshtml`.

                          ## Activate Theme

                          To use this theme in production, you need to configure it in your Articulate settings:

                          1. Open your Articulate root node in the Umbraco backoffice
                          2. Select **"Theme Name"** from the **Theme** dropdown
                          3. Save changes

                          ## Documentation

                          - Theme Guide: https://github.com/Shazwazza/Articulate/wiki/Themes
                          """;

            var readmePath = Path.Combine(themeRoot, "README.md");
            await File.WriteAllTextAsync(readmePath, readme);
        }

        private async Task ExtractResourceToFileAsync(
            Assembly assembly,
            string resourceName,
            string destinationFilePath)
        {
            if (Path.GetDirectoryName(destinationFilePath) is { } directoryPath)
            {
                _ = Directory.CreateDirectory(directoryPath);
            }
            else
            {
                logger.LogError(
                    "Could not determine a valid directory path from '{DestinationFilePath}' for '{ResourceName}'. Skipping file creation.",
                    destinationFilePath,
                    resourceName);
                return;
            }

            await using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                logger.LogError(
                    "Could not find resource stream for '{ResourceName}'. Skipping file creation.",
                    resourceName);
                return;
            }

            await using var fileStream = new FileStream(destinationFilePath, FileMode.Create);
            await stream.CopyToAsync(fileStream);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<string>?> GetAllThemesAsync() =>
            await appCaches.RuntimeCache.GetCacheItemAsync(
                AllThemesCacheKey,
                async () =>
                {
                    Task<IEnumerable<string>> defaultThemesTask = GetDefaultThemesAsync();
                    Task<IEnumerable<string>> userThemesTask = GetUserThemesAsync();
                    string[] packageThemeKeys = GetPackageThemeKeys().ToArray();

                    IEnumerable<string>[] themeKeyGroups =
                        await Task.WhenAll(defaultThemesTask, userThemesTask);

                    string[] defaultThemeKeys = themeKeyGroups[0].ToArray();
                    string[] userThemeKeys = themeKeyGroups[1].ToArray();
                    WarnForReservedUserThemeKeys(userThemeKeys);

                    return defaultThemeKeys
                        .Union(userThemeKeys)
                        .Union(packageThemeKeys)
                        .OrderBy(themeKey => themeKey, StringComparer.OrdinalIgnoreCase);
                },
                TimeSpan.FromSeconds(30));

        private static Assembly GetWebAssembly()
        {
            // 1. Try to find Articulate.Web if already loaded
            Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetName().Name == "Articulate.Web");

            // 2. Safety check
            return assembly ?? throw new InvalidOperationException(
                "Could not find 'Articulate.Web' assembly. Ensure the Articulate package is installed correctly.");
        }

        /// <inheritdoc/>
        public Task<IEnumerable<string>> GetDefaultThemesAsync() => Task.FromResult<IEnumerable<string>>(DefaultThemes.AllThemeNames);

        private static Task<IEnumerable<string>> GetThemesFromPhysicalPathAsync(string physicalPath) =>
            Task.FromResult(Directory.Exists(physicalPath)
                ? new DirectoryInfo(physicalPath).GetDirectories().Select(d => d.Name)
                : []);

        private Task<IEnumerable<string>> GetUserThemesAsync()
        {
            var physicalPath = Path.Combine(hostingEnvironment.ContentRootPath, Paths.UserThemesRoot);
            return GetThemesFromPhysicalPathAsync(physicalPath);
        }

        private void WarnForReservedUserThemeKeys(IEnumerable<string> userThemeKeys)
        {
            var builtInThemeKeys = new HashSet<string>(DefaultThemes.AllThemeNames, StringComparer.OrdinalIgnoreCase);

            foreach (string userThemeKey in userThemeKeys.Where(builtInThemeKeys.Contains))
            {
                logger.LogWarning(
                    "User Articulate theme key '{ThemeKey}' matches a built-in theme key. User theme views are searched before built-in theme views.",
                    userThemeKey);
            }
        }

        private IEnumerable<string> GetPackageThemeKeys()
        {
            var builtInThemeKeys = new HashSet<string>(DefaultThemes.AllThemeNames, StringComparer.OrdinalIgnoreCase);
            var packageThemeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IArticulateThemeDescriptorProvider provider in themeDescriptorProviders)
            {
                AddProviderThemeKeys(provider, builtInThemeKeys, packageThemeKeys);
            }

            return packageThemeKeys;
        }

        private void AddProviderThemeKeys(
            IArticulateThemeDescriptorProvider provider,
            HashSet<string> builtInThemeKeys,
            HashSet<string> packageThemeKeys)
        {
            foreach (string rawThemeKey in provider.GetThemeKeys())
            {
                if (!TryNormalizeThemeKey(provider, rawThemeKey, builtInThemeKeys, out string? themeKey))
                {
                    continue;
                }

                if (!packageThemeKeys.Add(themeKey))
                {
                    logger.LogWarning(
                        "Skipping duplicate Articulate theme key '{ThemeKey}' from provider {ProviderType}.",
                        themeKey,
                        provider.GetType().FullName);
                }
            }
        }

        private bool TryNormalizeThemeKey(
            IArticulateThemeDescriptorProvider provider,
            string rawThemeKey,
            HashSet<string> builtInThemeKeys,
            [NotNullWhen(true)] out string? themeKey)
        {
            if (string.IsNullOrWhiteSpace(rawThemeKey))
            {
                logger.LogWarning(
                    "Skipping an empty Articulate theme key from provider {ProviderType}.",
                    provider.GetType().FullName);
                themeKey = null;
                return false;
            }

            themeKey = rawThemeKey.Trim();

            if (!builtInThemeKeys.Contains(themeKey))
            {
                return true;
            }

            logger.LogWarning(
                "Skipping Articulate theme key '{ThemeKey}' from provider {ProviderType} because it is reserved for a built-in theme.",
                themeKey,
                provider.GetType().FullName);
            themeKey = null;
            return false;
        }
    }
}


