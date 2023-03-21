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
            foreach(var theme in DefaultThemes.AllThemes)
            {
                theme.CreateBundles(_bundleManager);
            }
        }

        public void Terminate()
        {
        }

        
    }

}
