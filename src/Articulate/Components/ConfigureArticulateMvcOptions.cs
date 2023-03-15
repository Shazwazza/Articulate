using Articulate.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Articulate.Components
{
    internal class ConfigureArticulateMvcOptions : IConfigureOptions<MvcOptions>
    {
        private readonly ArticulateFrontEndFilterConvention _articulateFrontEndFilterConvention;

        public ConfigureArticulateMvcOptions(ArticulateFrontEndFilterConvention articulateFrontEndFilterConvention)
        {
            _articulateFrontEndFilterConvention = articulateFrontEndFilterConvention;
        }

        public void Configure(MvcOptions options) => options.Conventions.Add(_articulateFrontEndFilterConvention);
    }
}
