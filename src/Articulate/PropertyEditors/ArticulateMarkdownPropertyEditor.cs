using System;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Templates;

namespace Articulate.PropertyEditors
{

    [DataEditor("Articulate.MarkdownEditor", "Articulate Markdown editor", "markdowneditor", ValueType = "TEXT")]
    public class ArticulateMarkdownPropertyEditor : MarkdownPropertyEditor
    {
        [Obsolete]
        public ArticulateMarkdownPropertyEditor(IDataValueEditorFactory dataValueEditorFactory, IIOHelper ioHelper)
            : base(dataValueEditorFactory, ioHelper)
        {
        }

        public ArticulateMarkdownPropertyEditor(IDataValueEditorFactory dataValueEditorFactory, IIOHelper ioHelper, IEditorConfigurationParser editorConfigurationParser)
            : base(dataValueEditorFactory, ioHelper, editorConfigurationParser)
        {
        }
    }

    // using a reasonable Markdown converter
    public class ArticulateMarkdownEditorValueConverter : MarkdownEditorValueConverter
    {
        public ArticulateMarkdownEditorValueConverter(HtmlLocalLinkParser localLinkParser, HtmlUrlParser urlParser) : base(localLinkParser, urlParser)
        {
        }

        public override bool IsConverter(IPublishedPropertyType propertyType)
            => "Articulate.MarkdownEditor" == propertyType.EditorAlias;

        public override object ConvertIntermediateToObject(
            IPublishedElement owner,
            IPublishedPropertyType propertyType,
            PropertyCacheLevel referenceCacheLevel,
            object inter,
            bool preview)
        {
            var md = (string)inter;
            return new HtmlEncodedString((inter == null) ? string.Empty : MarkdownHelper.ToHtml(md));
        }
    }
}
