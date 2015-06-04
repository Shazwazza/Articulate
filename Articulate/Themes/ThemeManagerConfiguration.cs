using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;

namespace Articulate.Themes
{
    class ThemeManagerConfiguration : ApplicationEventHandler
    {
     

        protected override void ApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            base.ApplicationInitialized(umbracoApplication, applicationContext);

            ThemeManagerResolver.Current = new ThemeManagerResolver(new DefaultThemeManager());
        }
    }
}