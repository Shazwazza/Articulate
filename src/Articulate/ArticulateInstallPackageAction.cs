using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core.PackageActions;
using Umbraco.Core.Composing;
using Umbraco.Web.Composing;
using Current = Umbraco.Web.Composing.Current;

namespace Articulate
{
    public class ArticulateInstallPackageAction : IPackageAction
    {
        public bool Execute(string packageName, XElement xmlData)
        {
            var dataInstaller = Current.Factory.GetInstance<ArticulateDataInstaller>();
            var root = dataInstaller.Execute(out var packageInstalled);
            //TODO: Maybe log something?
            return true;
        }

        public string Alias() => "articulateInstall";

        public bool Undo(string packageName, XElement xmlData) => true;
    }
}
