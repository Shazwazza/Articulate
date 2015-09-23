using System.Threading;
using Umbraco.Core.Configuration;

namespace Articulate.Options
{
    /// <summary>
    /// Adds the articulate options getter to the UmbracoConfig
    /// </summary>
    public static class UmbracoConfigExtensions
    {
        private static ArticulateOptions _options;

        public static ArticulateOptions ArticulateOptions(this UmbracoConfig umbracoConfig)
        {
            LazyInitializer.EnsureInitialized(ref _options, () => Options.ArticulateOptions.Default);

            return _options;
        } 

    }
}