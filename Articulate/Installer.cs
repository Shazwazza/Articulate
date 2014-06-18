using System;
using System.Net.Mime;
using System.Web.UI;
using System.Xml;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Models;
using umbraco.interfaces;

namespace Articulate
{

    //TODO: Make this do the actual installation of data, do not rely on the packager!

    public class Installer : IPackageAction
    {
        public bool Execute(string packageName, XmlNode xmlData)
        {
            var root = ApplicationContext.Current.Services.ContentTypeService
                .GetContentType("Articulate");

            var toPublish = ApplicationContext.Current.Services.ContentService
                .GetContentOfContentType(root.Id);

            //first publish everything
            foreach (var content in toPublish)
            {
                ApplicationContext.Current.Services.ContentService.PublishWithChildrenWithStatus(
                    content, 0, true);
            }

            var post = ApplicationContext.Current.Services.ContentTypeService
                .GetContentType("ArticulateMarkdown");

            var toSave = ApplicationContext.Current.Services.ContentService
                .GetContentOfContentType(post.Id);

            //re-save with tags
            foreach (var content in toSave)
            {
                var cats = content.Properties["categories"].Value.ToString().Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                content.SetTags("categories", cats, true, "ArticulateCategories");

                var tags = content.Properties["tags"].Value.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                content.SetTags("tags", tags, true, "ArticulateTags");

                ApplicationContext.Current.Services.ContentService.SaveAndPublishWithStatus(content);
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