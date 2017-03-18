using System;
using System.Xml;
using Umbraco.Core;
using Umbraco.Core.IO;

namespace Articulate
{
    /// <summary>
    /// This is here because the umb core on is broken and will keep adding the dashboard every time the package is installed
    /// 
    /// </summary>
    public sealed class ArticulateAddDashboardPackageAction : umbraco.interfaces.IPackageAction
    {
        public ArticulateAddDashboardPackageAction()
        {
            
        }

        public bool Execute(string packageName, XmlNode xmlData)
        {
            //this will need a complete section node to work... 

            if (xmlData.HasChildNodes)
            {
                string sectionAlias = xmlData.Attributes["dashboardAlias"].Value;
                string dbConfig = SystemFiles.DashboardConfig;

                XmlNode section = xmlData.SelectSingleNode("./section");
                XmlDocument dashboardFile = XmlHelper.OpenAsXmlDocument(dbConfig);
                
                //don't continue if it already exists
                var found = dashboardFile.SelectNodes("//section[@alias='" + sectionAlias + "']");
                if (found == null || found.Count <= 0)
                {
                    XmlNode importedSection = dashboardFile.ImportNode(section, true);

                    XmlAttribute alias = XmlHelper.AddAttribute(dashboardFile, "alias", sectionAlias);
                    importedSection.Attributes.Append(alias);

                    dashboardFile.DocumentElement.AppendChild(importedSection);

                    dashboardFile.Save(IOHelper.MapPath(dbConfig));
                }

                return true;
            }

            return false;
        }


        public string Alias()
        {
            return "articulateAddDashboardSection";
        }

        public bool Undo(string packageName, XmlNode xmlData)
        {

            string sectionAlias = xmlData.Attributes["dashboardAlias"].Value;
            string dbConfig = SystemFiles.DashboardConfig;
            XmlDocument dashboardFile = XmlHelper.OpenAsXmlDocument(dbConfig);

            XmlNode section = dashboardFile.SelectSingleNode("//section [@alias = '" + sectionAlias + "']");

            if (section != null)
            {

                dashboardFile.SelectSingleNode("/dashBoard").RemoveChild(section);
                dashboardFile.Save(IOHelper.MapPath(dbConfig));
            }

            return true;
        }

        public XmlNode SampleXml()
        {
            throw new NotImplementedException();
        }
        
    }
}