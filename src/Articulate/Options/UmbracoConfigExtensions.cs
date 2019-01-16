using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;

namespace Articulate.Options
{
    /// <summary>
    /// Adds the articulate options getter to the UmbracoConfig
    /// </summary>
    public static class UmbracoConfigExtensions
    {
        public static ArticulateOptions Articulate(this Configs configs) => Current.Configs.GetConfig<ArticulateOptions>();
    }
}