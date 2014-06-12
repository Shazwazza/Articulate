using System.Net.Mime;
using System.Xml;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using umbraco.interfaces;

namespace Articulate
{
    public class Installer : IPackageAction
    {
        public bool Execute(string packageName, XmlNode xmlData)
        {
            var root = ApplicationContext.Current.Services.ContentTypeService
                .GetContentType("Articulate");

            var post = ApplicationContext.Current.Services.ContentTypeService
                .GetContentType("ArticulateMarkdown");

            var toSave = ApplicationContext.Current.Services.ContentService
                .GetContentOfContentType(post.Id);

            foreach (var content in toSave)
            {
                ApplicationContext.Current.Services.ContentService.Save(content);
            }

            var toPublish = ApplicationContext.Current.Services.ContentService
                .GetContentOfContentType(root.Id);

            foreach (var content in toPublish)
            {
                ApplicationContext.Current.Services.ContentService.PublishWithChildrenWithStatus(
                    content, 0, true);    
            }
            
            return true;
        }

        public string Alias()
        {
            return typeof (Installer).FullName;
        }

        public bool Undo(string packageName, XmlNode xmlData)
        {
            return true;
        }

        public XmlNode SampleXml()
        {
            var xml = "<Action runat=\"install\" undo=\"true\" alias=\"" + typeof(Installer).FullName + "\" />";
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc.DocumentElement;
        }
    }
}