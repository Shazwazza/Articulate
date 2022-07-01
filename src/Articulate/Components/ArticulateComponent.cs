using Smidge;
using Smidge.Models;
using Umbraco.Cms.Core.Composing;
using Umbraco.Extensions;

namespace Articulate.Components
{
    public class ArticulateComponent : IComponent
    {
        private readonly IBundleManager _bundleManager;

        public ArticulateComponent(IBundleManager bundleManager)
        {
            _bundleManager = bundleManager;
        }

        public void Initialize()
        {
            _bundleManager.CreateJs("articulate-vapor-js", RequiredThemedJsFolder("VAPOR"));
            _bundleManager.CreateCss("articulate-vapor-css", RequiredThemedCssFolder("VAPOR"));
            
            _bundleManager.CreateJs("articulate-material-js", RequiredThemedJsFolder("Material"));
            _bundleManager.CreateCss("articulate-material-css", RequiredThemedCssFolder("Material"));
            
            _bundleManager.CreateCss("articulate-phantom-css", RequiredThemedCssFolder("Phantom"));
        }

        public void Terminate()
        {
        }

        private string RequiredThemedCss(string theme, string filePath)
            => PathHelper.GetThemePath(theme) + "Assets/css" + filePath.EnsureStartsWith('/');

        private string RequiredThemedJs(string theme, string filePath)
            => PathHelper.GetThemePath(theme) + "Assets/js" + filePath.EnsureStartsWith('/');

        private string RequiredThemedCssFolder(string theme)
            => PathHelper.GetThemePath(theme) + "Assets/css";

        private string RequiredThemedJsFolder(string theme)
            => PathHelper.GetThemePath(theme) + "Assets/js";
    }

}
