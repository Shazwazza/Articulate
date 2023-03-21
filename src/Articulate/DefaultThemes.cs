using System;
using System.Collections.Generic;
using System.Linq;
using Smidge;

namespace Articulate
{
    public sealed class DefaultThemes
    {
        private static readonly IReadOnlyDictionary<string, DefaultTheme> s_defaultThemes
            = new Dictionary<string, DefaultTheme>(StringComparer.InvariantCultureIgnoreCase)
            {
                [Vapor.Name] = new Vapor(),
                [Material.Name] = new Material(),
                [Phantom.Name] = new Phantom(),
                [Mini.Name] = new Mini()
            };

        public static DefaultTheme[] AllThemes { get; } = s_defaultThemes.Values.ToArray();

        public static bool IsDefaultTheme(string themeName)
            => s_defaultThemes.ContainsKey(themeName);

        public class Vapor : DefaultTheme
        {
            public const string Name = "VAPOR";

            public override void CreateBundles(IBundleManager bundleManager)
            {
                bundleManager.CreateJs("articulate-vapor-js", DefaultTheme.RequiredThemedJsFolder(Name));
                bundleManager.CreateCss("articulate-vapor-css", DefaultTheme.RequiredThemedCssFolder(Name));
            }
        }

        public class Material : DefaultTheme
        {
            public const string Name = "Material";

            public override void CreateBundles(IBundleManager bundleManager)
            {
                bundleManager.CreateJs("articulate-material-js", DefaultTheme.RequiredThemedJsFolder(Name));
                bundleManager.CreateCss("articulate-material-css", DefaultTheme.RequiredThemedCssFolder(Name));
            }
        }

        public class Phantom : DefaultTheme
        {
            public const string Name = "Phantom";

            public override void CreateBundles(IBundleManager bundleManager)
                => bundleManager.CreateCss("articulate-phantom-css", DefaultTheme.RequiredThemedCssFolder(Name));
        }

        public class Mini : DefaultTheme
        {
            public const string Name = "Mini";

            public override void CreateBundles(IBundleManager bundleManager)
                => bundleManager.CreateCss("articulate-mini-css", DefaultTheme.RequiredThemedCssFolder(Name));
        }

        public abstract class DefaultTheme
        {
            public abstract void CreateBundles(IBundleManager bundleManager);

            protected static string RequiredThemedCssFolder(string theme)
                => PathHelper.GetThemePath(theme) + "Assets/css";

            protected static string RequiredThemedJsFolder(string theme)
                => PathHelper.GetThemePath(theme) + "Assets/js";
        }
    }
}
