using System.Web.Routing;
using System.Xml.Linq;
using Articulate.Routing;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PackageActions;
using Current = Umbraco.Web.Composing.Current;

namespace Articulate.Packaging
{
    public class ArticulateInstallPackageAction : IPackageAction
    {
        public bool Execute(string packageName, XElement xmlData)
        {
            var dataInstaller = Current.Factory.GetInstance<ArticulateDataInstaller>();
            var root = dataInstaller.InstallContent();

            Current.Logger.Info<ArticulateInstallPackageAction>("Articulate data installation completed");

            var articulateRoutes = Current.Factory.GetInstance<ArticulateRoutes>();
            articulateRoutes.MapRoutes(RouteTable.Routes);

            return true;
        }

        public string Alias() => "articulateInstall";

        public bool Undo(string packageName, XElement xmlData) => true;
    }
}
