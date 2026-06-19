#nullable enable
using System.Xml;
using System.Xml.XPath;
using Argotic.Common;
using Argotic.Extensions;

namespace Articulate.Syndication.BlogML
{
    public class TagsSyndicationExtension() : SyndicationExtension("tags", Namespace, new Version("1.0")), IComparable
    {
        private const string Namespace = "https://github.com/Shazwazza/Articulate/blogml/";

        public TagsSyndicationExtensionContext Context
        {
            get;
        }
            = new();

        /// <inheritdoc />
        public int CompareTo(object? obj) =>
            obj switch
            {
                null => 1,
                TagsSyndicationExtension syndicationExtension =>
                    string.Compare(Description, syndicationExtension.Description, StringComparison.OrdinalIgnoreCase) |
                    Uri.Compare(
                        Documentation,
                        syndicationExtension.Documentation,
                        UriComponents.AbsoluteUri,
                        UriFormat.SafeUnescaped,
                        StringComparison.OrdinalIgnoreCase) |
                    string.Compare(Name, syndicationExtension.Name, StringComparison.OrdinalIgnoreCase) |
                    Version.CompareTo(syndicationExtension.Version) |
                    string.Compare(XmlNamespace, syndicationExtension.XmlNamespace, StringComparison.Ordinal) |
                    string.Compare(XmlPrefix, syndicationExtension.XmlPrefix, StringComparison.Ordinal) |
                    ComparisonUtility.CompareSequence(
                        Context.Tags,
                        syndicationExtension.Context.Tags,
                        StringComparison.OrdinalIgnoreCase),
                _ => throw new ArgumentException(
                    string.Format(
                        null,
                        @"obj is not of type {0}, type was found to be '{1}'.",
                        GetType().FullName,
                        obj.GetType().FullName),
                    nameof(obj))
            };

        /// <inheritdoc />
        public override bool Load(IXPathNavigable source)
        {
            Guard.ArgumentNotNull(source, "source");
            XPathNavigator? navigator = source.CreateNavigator();
            if (navigator is null)
            {
                return false;
            }

            var flag = Context.Load(navigator, CreateNamespaceManager(navigator));
            OnExtensionLoaded(new SyndicationExtensionLoadedEventArgs(source, this));
            return flag;
        }

        /// <inheritdoc />
        public override bool Load(XmlReader reader)
        {
            Guard.ArgumentNotNull(reader, "reader");
            return Load(new XPathDocument(reader).CreateNavigator());
        }

        /// <inheritdoc />
        public override void WriteTo(XmlWriter writer)
        {
            Guard.ArgumentNotNull(writer, "writer");
            Context.WriteTo(writer, XmlNamespace);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            using var memoryStream = new MemoryStream();
            using (var writer = XmlWriter.Create(
                       memoryStream,
                       new XmlWriterSettings
                       {
                           ConformanceLevel = ConformanceLevel.Fragment, Indent = true, OmitXmlDeclaration = true
                       }))
            {
                WriteTo(writer);
            }

            _ = memoryStream.Seek(0L, SeekOrigin.Begin);
            using var streamReader = new StreamReader(memoryStream);
            return streamReader.ReadToEnd();
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is not TagsSyndicationExtension)
            {
                return false;
            }

            return CompareTo(obj) == 0;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Description);
            hash.Add(Documentation);
            hash.Add(Name);
            hash.Add(Version);
            hash.Add(XmlNamespace);
            hash.Add(XmlPrefix);
            if (Context.Tags is null || Context.Tags.Count == 0)
            {
                return hash.ToHashCode();
            }

            foreach (var tag in Context.Tags)
            {
                hash.Add(tag);
            }

            return hash.ToHashCode();
        }
    }
}
