using System.Xml.Linq;
using Umbraco.Core.PackageActions;
using Umbraco.Core;
using Current = Umbraco.Web.Composing.Current;

namespace Articulate
{
    public class ArticulateInstallPackageAction : IPackageAction
    {
        public bool Execute(string packageName, XElement xmlData)
        {
            var dataInstaller = Current.Factory.GetInstance<ArticulateDataInstaller>();
            var root = dataInstaller.Execute();
            //TODO: Maybe log something?
            return true;
        }

        public string Alias() => "articulateInstall";

        public bool Undo(string packageName, XElement xmlData) => true;
    }
}
