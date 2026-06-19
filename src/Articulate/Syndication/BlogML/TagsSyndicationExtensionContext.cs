#nullable enable
using System.Collections.ObjectModel;
using System.Xml;
using System.Xml.XPath;
using Argotic.Common;

namespace Articulate.Syndication.BlogML
{
    [Serializable]
    public class TagsSyndicationExtensionContext
    {
        private Collection<string> _extensionTags = [];

        public Collection<string> Tags
        {
            get => _extensionTags;
            set => _extensionTags = value;
        }

        public bool Load(XPathNavigator source, XmlNamespaceManager manager)
        {
            var flag = false;
            Guard.ArgumentNotNull(source, "source");
            Guard.ArgumentNotNull(manager, "manager");
            if (!source.HasChildren)
            {
                return flag;
            }

            XPathNavigator? xpathNavigator = source.SelectSingleNode("tags");
            if (xpathNavigator is null)
            {
                return flag;
            }

            XPathNodeIterator xpathTagIterator = source.Select("tag");
            if (xpathTagIterator.Count <= 0)
            {
                return flag;
            }

            while (xpathTagIterator.MoveNext())
            {
                if (xpathTagIterator.Current is { HasAttributes: true })
                {
                    var tag = xpathTagIterator.Current.GetAttribute("ref", manager.DefaultNamespace);
                    if (!string.IsNullOrEmpty(tag))
                    {
                        Tags.Add(tag);
                    }
                }

                flag = true;
            }

            return flag;
        }

        public void WriteTo(XmlWriter writer, string xmlNamespace)
        {
            Guard.ArgumentNotNull(writer, "writer");
            Guard.ArgumentNotNullOrEmptyString(xmlNamespace, "xmlNamespace");
            if (Tags.Count <= 0)
            {
                return;
            }

            writer.WriteStartElement("tags");
            foreach (var tag in Tags)
            {
                writer.WriteStartElement("tag");
                writer.WriteAttributeString("ref", tag);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }
    }
}
