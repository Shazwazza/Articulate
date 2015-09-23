using System;
using System.Linq;
using System.Xml;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using umbraco.interfaces;

namespace Articulate
{
    //We cannot activate this until this is fixed:
    // http://issues.umbraco.org/issue/U4-5121
    // U4-5121 umbraco.content launches threadpool threads to reload the xml cache which causes lots of other issues
    // until then we need to use the usercontrol to do the install

    public class ArcticulateInstallerPackageAction : IPackageAction
    {
        public bool Execute(string packageName, XmlNode xmlData)
        {
            var dataInstaller = new ArticulateDataInstaller();
            var root = dataInstaller.Execute();

            return true;
        }

        public string Alias()
        {
            return "ArcticulateInstaller";
        }

        public bool Undo(string packageName, XmlNode xmlData)
        {
            return true;
        }

        public XmlNode SampleXml()
        {
            var xml = "<Action runat=\"install\" undo=\"false\" alias=\"" + "ArcticulateInstaller" + "\" />";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc.DocumentElement;
        }
    }
}