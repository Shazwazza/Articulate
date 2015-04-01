using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;

namespace Articulate.Themes
{
    public class DefaultThemeManager : IThemeManager
    {
        private static Dictionary<string, string> _packageThemes = new Dictionary<string, string>();

        public void AddOrUpdatePackageTheme(string package, string theme)
        {
            throw new NotImplementedException();
            //using (Resolution.Configuration) 
            //{
            //}
        }

        public bool RemovePackageTheme(string theme)
        {
            throw new NotImplementedException();
        }
    }
}
